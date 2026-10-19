using Identity.Application.Abstractions.Cqrs;
using Identity.Domain.Entities;

namespace Identity.Application.Features.Users.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId, Guid CurrentSessionId) : IQuery<UserProfile>;

public sealed record UserProfile(Guid Id, string Email, string? DisplayName, bool EmailVerified, string Status, DateTimeOffset CreatedAt)
{
    public static UserProfile FromActive(User user) => new(user.Id, user.Email, user.DisplayName, user.EmailVerifiedAt is not null, "active", user.CreatedAt);
}
