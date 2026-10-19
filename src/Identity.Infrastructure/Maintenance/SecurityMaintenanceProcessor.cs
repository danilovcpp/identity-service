using Identity.Domain.Enums;
using Identity.Infrastructure.Observability;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Maintenance;

public sealed class SecurityMaintenanceProcessor(IdentityDbContext context, IOptions<SecurityMaintenanceOptions> options, AuthMetrics metrics, TimeProvider clock)
{
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var auditBefore = now.AddDays(-settings.AuditRetentionDays);
        var tokenBefore = now.AddDays(-settings.TokenRetentionDays);
        var outboxBefore = now.AddDays(-settings.ProcessedOutboxRetentionDays);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // Bounded independent batches, with SKIP LOCKED so replicas and delivery never wait on each other.
        var deleted = await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM security_audit_events WHERE id IN (SELECT id FROM security_audit_events WHERE occurred_at < {auditBefore} ORDER BY occurred_at, id LIMIT {settings.BatchSize} FOR UPDATE SKIP LOCKED)", cancellationToken);
        deleted += await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM one_time_tokens WHERE id IN (SELECT id FROM one_time_tokens WHERE expires_at < {tokenBefore} ORDER BY expires_at, id LIMIT {settings.BatchSize} FOR UPDATE SKIP LOCKED)", cancellationToken);
        deleted += await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM outbox_messages WHERE id IN (SELECT id FROM outbox_messages WHERE processed_at < {outboxBefore} ORDER BY processed_at, id LIMIT {settings.BatchSize} FOR UPDATE SKIP LOCKED)", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var active = await (from session in context.Sessions.AsNoTracking()
                            join user in context.Users.AsNoTracking() on session.UserId equals user.Id
                            where user.Status == UserStatus.Active && session.RevokedAt == null && session.Audience != null
                                && session.CreatedAt <= now && session.AbsoluteExpiresAt > now
                            select session.Id).LongCountAsync(cancellationToken);
        metrics.Sessions(active, now);
        return deleted;
    }
}
