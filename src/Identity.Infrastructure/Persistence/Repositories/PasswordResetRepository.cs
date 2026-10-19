using Identity.Application.Abstractions.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

internal sealed class PasswordResetRepository(IdentityDbContext context) : IPasswordResetRepository
{
    public Task<Guid?> FindOwnerAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.OneTimeTokens.AsNoTracking().Where(token => token.TokenHash == tokenHash && token.Purpose == OneTimeTokenPurpose.PasswordReset)
            .Select(token => (Guid?)token.UserId).SingleOrDefaultAsync(cancellationToken);

    public Task<OneTimeToken?> FindForUpdateAsync(string tokenHash, CancellationToken cancellationToken)
    {
        RequireTransaction();
        return context.OneTimeTokens.FromSqlInterpolated($"SELECT *, xmin FROM one_time_tokens WHERE token_hash = CAST({tokenHash} AS character(64)) AND purpose = {(short)OneTimeTokenPurpose.PasswordReset} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task InvalidateOutstandingAsync(Guid userId, DateTimeOffset now, Guid? exceptTokenId, CancellationToken cancellationToken)
    {
        RequireTransaction();
        await context.OneTimeTokens.Where(token => token.UserId == userId && token.Purpose == OneTimeTokenPurpose.PasswordReset
            && token.ConsumedAt == null && token.ExpiresAt > now && (exceptTokenId == null || token.Id != exceptTokenId))
            // Also invalidate tokens issued by a replica whose clock is slightly ahead, without violating timestamp constraints.
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.ConsumedAt, token => token.CreatedAt > now ? token.CreatedAt : now), cancellationToken);
    }

    public void Add(OneTimeToken token) => context.OneTimeTokens.Add(token);
    public void Add(OutboxMessage message) => context.OutboxMessages.Add(message);
    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null) { throw new InvalidOperationException("Password reset mutations require an explicit transaction."); }
    }
}
