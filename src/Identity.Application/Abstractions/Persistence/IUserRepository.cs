using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Persistence;

public interface IUserRepository
{
    void Add(User user);
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
}
