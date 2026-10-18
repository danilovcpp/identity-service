using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Persistence;

public interface IEmailVerificationRepository
{
    void Add(OneTimeToken token);
    void Add(OutboxMessage message);
    void Add(SecurityAuditEvent auditEvent);
    Task<OneTimeToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);
}
