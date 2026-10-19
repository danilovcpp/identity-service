using Identity.Domain.Common;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class User
{
    private User() { }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = null!;
    public string? DisplayName { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }
    public Guid? DisabledBy { get; private set; }
    public string? DisableReason { get; private set; }
    public UserStatus? StatusBeforeDisable { get; private set; }

    public static User Create(string email, string? displayName, DateTimeOffset now)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = EmailAddress.Canonicalize(email),
            NormalizedEmail = EmailAddress.Normalize(email),
            Status = UserStatus.PendingVerification,
            CreatedAt = now.ToUniversalTime(),
            UpdatedAt = now.ToUniversalTime()
        };
        user.SetDisplayName(displayName, now);
        return user;
    }

    public void VerifyEmail(DateTimeOffset now)
    {
        DomainRules.Require(Status == UserStatus.PendingVerification, "invalid_user_state", "The user cannot be verified in this state.");
        UpdatedAt = DomainRules.Time(now, UpdatedAt);
        EmailVerifiedAt = UpdatedAt;
        Status = UserStatus.Active;
    }

    public static User CreateInvited(string email, string? displayName, DateTimeOffset now)
    {
        var user = Create(email, displayName, now);
        user.Status = UserStatus.Invited;
        return user;
    }

    public void Disable(DateTimeOffset now)
    {
        DomainRules.Require(Status is UserStatus.Active or UserStatus.PendingVerification or UserStatus.Invited,
            "invalid_user_state", "The user cannot be disabled in this state.");
        UpdatedAt = DomainRules.Time(now, UpdatedAt);
        StatusBeforeDisable = Status;
        DisabledAt = UpdatedAt;
        Status = UserStatus.Disabled;
    }

    public void Disable(DateTimeOffset now, Guid administratorId, string reason)
    {
        var actor = DomainRules.Identifier(administratorId);
        var text = DomainRules.Text(reason.Trim(), 500);
        Disable(now);
        DisabledBy = actor;
        DisableReason = text;
    }

    public void Enable(DateTimeOffset now)
    {
        DomainRules.Require(Status == UserStatus.Disabled, "invalid_user_state", "Only a disabled user can be enabled.");
        var previous = StatusBeforeDisable ?? (EmailVerifiedAt is null ? UserStatus.PendingVerification : UserStatus.Active);
        DomainRules.Require((previous is UserStatus.Active or UserStatus.PendingVerification or UserStatus.Invited)
            && (previous == UserStatus.Active) == (EmailVerifiedAt is not null), "invalid_user_state", "The previous user state is invalid.");
        UpdatedAt = DomainRules.Time(now, UpdatedAt);
        Status = previous;
        StatusBeforeDisable = null;
        DisabledAt = null;
        DisabledBy = null;
        DisableReason = null;
    }

    public void AcceptInvitation(DateTimeOffset now)
    {
        DomainRules.Require(Status == UserStatus.Invited && EmailVerifiedAt is null,
            "invalid_user_state", "The invitation cannot be accepted in this state.");
        UpdatedAt = DomainRules.Time(now, UpdatedAt);
        EmailVerifiedAt = UpdatedAt;
        Status = UserStatus.Active;
    }

    public void Deactivate(DateTimeOffset now)
    {
        DomainRules.Require(Status != UserStatus.Deactivated, "invalid_user_state", "The user is already deactivated.");
        UpdatedAt = DomainRules.Time(now, UpdatedAt);
        DeactivatedAt = UpdatedAt;
        Status = UserStatus.Deactivated;
        StatusBeforeDisable = null;
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    public void SetDisplayName(string? displayName, DateTimeOffset now)
    {
        DomainRules.Require(Status != UserStatus.Deactivated, "invalid_user_state", "A deactivated user cannot be changed.");
        var value = displayName is null ? null : DomainRules.Text(displayName.Trim(), 120);
        UpdatedAt = DomainRules.Time(now, UpdatedAt);
        DisplayName = value;
    }

    public void RecordFailedLogin(DateTimeOffset now, DateTimeOffset? lockedUntil)
    {
        DomainRules.Require(Status != UserStatus.Deactivated && FailedLoginCount < int.MaxValue,
            "invalid_user_state", "A login failure cannot be recorded in this state.");
        var timestamp = DomainRules.Time(now, UpdatedAt);
        DomainRules.Require(lockedUntil is null || lockedUntil > timestamp, "invalid_expiry", "The lock must expire in the future.");
        UpdatedAt = timestamp;
        FailedLoginCount++;
        LockedUntil = lockedUntil?.ToUniversalTime();
    }

    public void ResetLoginFailures(DateTimeOffset now)
    {
        DomainRules.Require(Status == UserStatus.Active, "invalid_user_state", "Only an active user can reset login failures.");
        UpdatedAt = DomainRules.Time(now, UpdatedAt);
        FailedLoginCount = 0;
        LockedUntil = null;
    }
}
