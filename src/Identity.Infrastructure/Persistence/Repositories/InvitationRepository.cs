using Identity.Application.Abstractions.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

internal sealed class InvitationRepository(IdentityDbContext context) : IInvitationRepository
{
    public Task<Guid?> FindOwnerAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.OneTimeTokens.AsNoTracking().Where(token => token.TokenHash == tokenHash && token.Purpose == OneTimeTokenPurpose.Invitation)
            .Select(token => (Guid?)token.UserId).SingleOrDefaultAsync(cancellationToken);

    public Task<OneTimeToken?> FindForUpdateAsync(string tokenHash, CancellationToken cancellationToken)
    {
        RequireTransaction();
        return context.OneTimeTokens.FromSqlInterpolated($"SELECT *, xmin FROM one_time_tokens WHERE token_hash = CAST({tokenHash} AS character(64)) AND purpose = {(short)OneTimeTokenPurpose.Invitation} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task InvalidateOutstandingAsync(Guid userId, DateTimeOffset now, Guid? exceptTokenId, CancellationToken cancellationToken) =>
        InvalidateAsync(userId, now, exceptTokenId, false, cancellationToken);

    public Task InvalidateAllPurposesAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        InvalidateAsync(userId, now, null, true, cancellationToken);

    private async Task InvalidateAsync(Guid userId, DateTimeOffset now, Guid? exceptTokenId, bool allPurposes, CancellationToken cancellationToken)
    {
        RequireTransaction();
        await context.OneTimeTokens.Where(token => token.UserId == userId && (allPurposes || token.Purpose == OneTimeTokenPurpose.Invitation)
            && token.ConsumedAt == null && token.ExpiresAt > now && (exceptTokenId == null || token.Id != exceptTokenId))
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.ConsumedAt, token => token.CreatedAt > now ? token.CreatedAt : now), cancellationToken);
    }

    public void Add(OneTimeToken token) => context.OneTimeTokens.Add(token);
    public void Add(OutboxMessage message) => context.OutboxMessages.Add(message);
    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null) { throw new InvalidOperationException("Invitation mutations require an explicit transaction."); }
    }
}
