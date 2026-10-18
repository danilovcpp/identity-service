using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Maintenance;

public sealed class SessionCleanupProcessor(IdentityDbContext context, IOptions<SessionCleanupOptions> options, TimeProvider clock)
{
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddSeconds(-options.Value.RetentionSeconds);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // Keep the entire chain until absolute session expiry + retention, including consumed and revoked tokens.
        var expired = await context.Sessions.FromSqlInterpolated($"SELECT *, xmin FROM sessions WHERE absolute_expires_at < {cutoff} ORDER BY absolute_expires_at, id LIMIT {options.Value.BatchSize} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);
        foreach (var session in expired)
        {
            // One statement removes an entire self-referencing family. Its FK uses NO ACTION (checked at statement end).
            await context.RefreshTokens.Where(token => token.SessionId == session.Id).ExecuteDeleteAsync(cancellationToken);
            context.Sessions.Remove(session);
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return expired.Count;
    }
}
