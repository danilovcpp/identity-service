using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Persistence;

public interface IInvitationRepository
{
    Task<Guid?> FindOwnerAsync(string tokenHash, CancellationToken cancellationToken);
    Task<OneTimeToken?> FindForUpdateAsync(string tokenHash, CancellationToken cancellationToken);
    Task InvalidateOutstandingAsync(Guid userId, DateTimeOffset now, Guid? exceptTokenId, CancellationToken cancellationToken);
    Task InvalidateAllPurposesAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(OneTimeToken token);
    void Add(OutboxMessage message);
}
