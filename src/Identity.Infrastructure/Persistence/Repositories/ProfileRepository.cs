using Identity.Application.Abstractions.Persistence;
using Identity.Application.Features.Users.GetCurrentUser;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

internal sealed class ProfileRepository(IdentityDbContext context) : IProfileRepository
{
    public Task<UserProfile?> GetCurrentAsync(Guid userId, Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken) =>
        (from user in context.Users.AsNoTracking()
         join session in context.Sessions.AsNoTracking() on user.Id equals session.UserId
         where user.Id == userId && user.Status == UserStatus.Active && session.Id == sessionId && session.RevokedAt == null
             && session.Audience != null && session.CreatedAt <= now && session.AbsoluteExpiresAt > now
         select new UserProfile(user.Id, user.Email, user.DisplayName, user.EmailVerifiedAt != null, "active", user.CreatedAt))
        .SingleOrDefaultAsync(cancellationToken);
}
