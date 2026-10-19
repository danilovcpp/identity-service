using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Users.GetUserDetails;

public sealed record GetUserDetailsQuery(Guid UserId) : IQuery<AdminUserDetails>;

public sealed record AdminUserDetails(Guid Id, string Email, string? DisplayName, string Status, bool EmailVerified,
    DateTimeOffset? EmailVerifiedAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? DeactivatedAt,
    DateTimeOffset? DisabledAt, Guid? DisabledBy, string? DisableReason, int FailedLoginCount, DateTimeOffset? LockedUntil);
