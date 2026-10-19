using Identity.Application.Features.Admin.Auth.Login;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Exceptions;
using Identity.Infrastructure.Security;

namespace Identity.UnitTests;

public sealed class AdminDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AdministratorRequiresActiveVerifiedUserAndKnownRole()
    {
        var user = User.Create("admin@identity.example", null, Now);
        Assert.Throws<DomainException>(() => IdentityAdministrator.Grant(user, AdministratorRole.IdentityAdministrator, null, Now));
        user.VerifyEmail(Now);
        Assert.Equal(user.Id, IdentityAdministrator.Grant(user, AdministratorRole.IdentityAdministrator, null, Now).UserId);
        Assert.Throws<DomainException>(() => IdentityAdministrator.Grant(user, (AdministratorRole)9, null, Now));
        user.Disable(Now);
        Assert.Throws<DomainException>(() => IdentityAdministrator.Grant(user, AdministratorRole.IdentityReadOnly, null, Now));
    }

    [Fact]
    public void SessionsHaveStrictIdleAndAbsoluteBoundariesAndCannotBeResurrected()
    {
        var idle = TimeSpan.FromMinutes(30);
        var session = AdminSession.Create(Guid.NewGuid(), Now, Now.AddHours(8));
        Assert.True(session.IsActive(Now, idle));
        Assert.False(session.IsActive(Now.AddTicks(-1), idle));
        Assert.False(session.IsActive(Now.Add(idle), idle));
        Assert.Throws<DomainException>(() => session.Touch(Now.Add(idle), idle));
        for (var minute = 29; minute < 480; minute += 29) { session.Touch(Now.AddMinutes(minute), idle); }
        Assert.False(session.IsActive(Now.AddHours(8), idle));
        Assert.Throws<DomainException>(() => AdminSession.Create(Guid.NewGuid(), Now, Now.AddHours(8).AddTicks(1)));
        var revoked = AdminSession.Create(Guid.NewGuid(), Now, Now.AddHours(1));
        revoked.Revoke(Now);
        revoked.Revoke(Now.AddMinutes(1));
        Assert.Equal(Now, revoked.RevokedAt);
        Assert.False(revoked.IsActive(Now, idle));
    }

    [Fact]
    public void InvitedAndInvitationPurposeAreNewValuesWithSeventyTwoHourLimit()
    {
        Assert.Equal(4, (short)UserStatus.Invited);
        Assert.Equal(2, (short)OneTimeTokenPurpose.Invitation);
        var invited = User.CreateInvited("invite@identity.example", "Invited", Now);
        Assert.Null(invited.EmailVerifiedAt);
        Assert.Equal(UserStatus.Invited, invited.Status);
        Assert.Throws<DomainException>(() => invited.VerifyEmail(Now));
        var token = OneTimeToken.Create(invited.Id, OneTimeTokenPurpose.Invitation, new string('a', 64), Now, Now.AddHours(72));
        token.Consume(Now);
        Assert.False(token.IsUsable(Now));
        Assert.Throws<DomainException>(() => OneTimeToken.Create(invited.Id, OneTimeTokenPurpose.Invitation,
            new string('b', 64), Now, Now.AddHours(72).AddTicks(1)));
    }

    [Theory]
    [InlineData(0, 28800)]
    [InlineData(1801, 28800)]
    [InlineData(1800, 28801)]
    [InlineData(1800, 60)]
    public void AdminConfigurationRejectsUnsafeTimeouts(int idle, int absolute) =>
        Assert.False(AdminAccessOptions.IsValid(new AdminAccessOptions { IdleTimeoutSeconds = idle, AbsoluteLifetimeSeconds = absolute }));

    [Fact]
    public async Task AdminValidatorBoundsInputAndCommandsDoNotPrintPasswords()
    {
        var command = new AdminLoginCommand("bad\nemail", new string('x', 3000));
        Assert.Equal(2, (await new AdminLoginValidator().Validate(command, CancellationToken.None)).Count);
        Assert.DoesNotContain(command.Password, command.ToString()!);
        await Assert.ThrowsAsync<OperationCanceledException>(() => new AdminLoginValidator().Validate(command, new CancellationToken(true)));
        Assert.False(AdminAccessOptions.IsValid(new AdminAccessOptions { Enabled = true, DataProtectionKeyPath = "relative/path" }));
    }
}
