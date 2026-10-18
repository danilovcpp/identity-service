using Identity.Application.Abstractions.Persistence;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

internal sealed class CredentialRepository(IdentityDbContext context) : ICredentialRepository
{
    public void Add(Credential credential) => context.Credentials.Add(credential);

    public Task<Credential?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Credentials.SingleOrDefaultAsync(credential => credential.UserId == userId, cancellationToken);
}
