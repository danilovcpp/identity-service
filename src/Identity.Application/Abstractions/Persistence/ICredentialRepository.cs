using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Persistence;

public interface ICredentialRepository
{
    void Add(Credential credential);
    Task<Credential?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}
