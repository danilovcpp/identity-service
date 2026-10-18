using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Persistence;

public interface ILoginRepository
{
    void Add(Session session);
    void Add(RefreshToken token);
    void Add(SecurityAuditEvent auditEvent);
    Task<bool> IsActiveAsync(Guid userId, Guid sessionId, string clientId, string audience, DateTimeOffset now, CancellationToken cancellationToken);
}
