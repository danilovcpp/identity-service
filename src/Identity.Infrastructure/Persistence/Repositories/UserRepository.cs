using Identity.Application.Abstractions.Persistence;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    public void Add(User user) => context.Users.Add(user);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<User?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireTransaction();
        return context.Users.FromSqlInterpolated($"SELECT *, xmin FROM users WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<User?> FindByNormalizedEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        RequireTransaction();
        return context.Users.FromSqlInterpolated($"SELECT *, xmin FROM users WHERE normalized_email = {normalizedEmail} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null) { throw new InvalidOperationException("Row locks require an explicit transaction."); }
    }
}
