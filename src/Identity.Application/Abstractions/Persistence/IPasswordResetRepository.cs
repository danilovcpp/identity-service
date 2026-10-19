using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Persistence;

public interface IPasswordResetRepository
{
    Task<Guid?> FindOwnerAsync(string tokenHash, CancellationToken cancellationToken);
    Task<OneTimeToken?> FindForUpdateAsync(string tokenHash, CancellationToken cancellationToken);
    Task InvalidateOutstandingAsync(Guid userId, DateTimeOffset now, Guid? exceptTokenId, CancellationToken cancellationToken);
    void Add(OneTimeToken token);
    void Add(OutboxMessage message);
}
