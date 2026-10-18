using Identity.Application.Features.Sessions.GetSessions;
using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Persistence;

public interface ISessionRepository
{
    Task<RefreshOwner?> FindRefreshOwnerAsync(string tokenHash, CancellationToken cancellationToken);
    Task<Session?> FindOwnedForUpdateAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Session>> FindAllForUpdateAsync(Guid userId, CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshAsync(string tokenHash, CancellationToken cancellationToken);
    Task RevokeTokensAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionSummary>> GetActiveAsync(Guid userId, Guid currentSessionId, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(RefreshToken token);
    void Add(SecurityAuditEvent auditEvent);
}

public sealed record RefreshOwner(Guid UserId, Guid SessionId);
