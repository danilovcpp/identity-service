using Identity.Application.Abstractions.Persistence;
using Identity.Application.Features.Sessions.GetSessions;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

internal sealed class SessionRepository(IdentityDbContext context) : ISessionRepository
{
    public Task<RefreshOwner?> FindRefreshOwnerAsync(string tokenHash, CancellationToken cancellationToken) =>
        (from token in context.RefreshTokens.AsNoTracking()
         join session in context.Sessions.AsNoTracking() on token.SessionId equals session.Id
         where token.TokenHash == tokenHash
         select new RefreshOwner(session.UserId, session.Id)).SingleOrDefaultAsync(cancellationToken);

    public Task<Session?> FindOwnedForUpdateAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        RequireTransaction();
        return context.Sessions.FromSqlInterpolated($"SELECT *, xmin FROM sessions WHERE id = {sessionId} AND user_id = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Session>> FindAllForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        RequireTransaction();
        return await context.Sessions.FromSqlInterpolated($"SELECT *, xmin FROM sessions WHERE user_id = {userId} AND revoked_at IS NULL ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    public Task<RefreshToken?> FindRefreshAsync(string tokenHash, CancellationToken cancellationToken)
    {
        RequireTransaction();
        return context.RefreshTokens.FromSqlInterpolated($"SELECT *, xmin FROM refresh_tokens WHERE token_hash = CAST({tokenHash} AS character(64)) FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task RevokeTokensAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        RequireTransaction();
        // Bulk update shares the use-case transaction. Do not subsequently modify tracked token objects in this scope.
        await context.RefreshTokens.Where(token => token.SessionId == sessionId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    public async Task<IReadOnlyList<SessionSummary>> GetActiveAsync(Guid userId, Guid currentSessionId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.Sessions.AsNoTracking().Where(session => session.UserId == userId && session.RevokedAt == null
            && session.Audience != null && session.CreatedAt <= now && session.AbsoluteExpiresAt > now)
            .OrderByDescending(session => session.LastUsedAt).ThenBy(session => session.Id)
            .Select(session => new SessionSummary(session.Id, session.ClientId, session.CreatedAt, session.LastUsedAt,
                session.AbsoluteExpiresAt, session.UserAgent, session.Id == currentSessionId)).ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => context.RefreshTokens.Add(token);
    public void Add(SecurityAuditEvent auditEvent) => context.SecurityAuditEvents.Add(auditEvent);

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null) { throw new InvalidOperationException("Session mutations require an explicit transaction."); }
    }
}
