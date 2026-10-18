using Identity.Application.Abstractions.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

internal sealed class LoginRepository(IdentityDbContext context) : ILoginRepository
{
    public void Add(Session session) => context.Sessions.Add(session);
    public void Add(RefreshToken token) => context.RefreshTokens.Add(token);
    public void Add(SecurityAuditEvent auditEvent) => context.SecurityAuditEvents.Add(auditEvent);

    public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, string clientId, string audience, DateTimeOffset now, CancellationToken cancellationToken) =>
        (from session in context.Sessions.AsNoTracking()
         join user in context.Users.AsNoTracking() on session.UserId equals user.Id
         where session.Id == sessionId && user.Id == userId && user.Status == UserStatus.Active
             && session.ClientId == clientId && session.Audience == audience && session.RevokedAt == null && session.CreatedAt <= now && session.AbsoluteExpiresAt > now
         select session.Id).AnyAsync(cancellationToken);
}
