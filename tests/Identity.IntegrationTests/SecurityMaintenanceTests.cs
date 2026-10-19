using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Maintenance;
using Identity.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.IntegrationTests;

public sealed class SecurityMaintenanceTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
    public async Task InitializeAsync()
    {
        await using var context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE users, credentials, sessions, refresh_tokens, one_time_tokens, outbox_messages, security_audit_events CASCADE");
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task Seed()
    {
        var created = Now.AddDays(-100);
        var user = User.Create("maintenance@identity.example", null, created);
        user.VerifyEmail(created);
        await using var context = database.CreateContext();
        context.Add(user);
        foreach (var offset in new[] { -1, 0, 1 })
        {
            context.Add(SecurityAuditEvent.Create(SecurityEventType.LoginSuccess, user.Id, "native", "trace", new AuditMetadata(), Now.AddDays(-90).AddSeconds(offset)));
            var expires = Now.AddDays(-1).AddSeconds(offset);
            context.Add(OneTimeToken.Create(user.Id, OneTimeTokenPurpose.PasswordReset, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), expires.AddMinutes(-30), expires));
            var message = OutboxMessage.Create("password_reset", Payload(), Now.AddDays(-8));
            message.MarkProcessed(Now.AddDays(-7).AddSeconds(offset));
            context.Add(message);
        }
        context.Add(OneTimeToken.Create(user.Id, OneTimeTokenPurpose.PasswordReset, new string('a', 64), Now.AddMinutes(-1), Now.AddMinutes(29)));
        var pending = OutboxMessage.Create("password_reset", Payload(), created);
        pending.RecordFailure("smtp_unavailable", created, created.AddMinutes(1));
        context.Add(pending);
        var session = Session.Create(user.Id, "native", null, Now.AddDays(-1), Now.AddDays(1), "identity-native");
        context.Add(session);
        context.Add(RefreshToken.Create(session, new string('b', 64), Now.AddDays(-1), Now.AddDays(1)));
        await context.SaveChangesAsync();
    }
    private static EncryptedEmailPayload Payload() => new("test", Convert.ToBase64String(new byte[12]), "AQ==", Convert.ToBase64String(new byte[16]));
    private static SecurityMaintenanceProcessor Processor(Identity.Infrastructure.Persistence.IdentityDbContext context, int batch = 100) =>
        new(context, Options.Create(new SecurityMaintenanceOptions { BatchSize = batch }), new AuthMetrics(NullLogger<AuthMetrics>.Instance), new FixedClock());

    [Fact]
    public async Task CleanupRespectsStrictRetentionBoundariesAndNeverTouchesPendingDeliveryOrRefreshEvidence()
    {
        await Seed();
        await using var context = database.CreateContext();
        Assert.Equal(3, await Processor(context).ProcessBatchAsync(CancellationToken.None));
        Assert.Equal(2, await context.SecurityAuditEvents.CountAsync());
        Assert.Equal(3, await context.OneTimeTokens.CountAsync());
        Assert.Equal(3, await context.OutboxMessages.CountAsync());
        Assert.NotNull((await context.OutboxMessages.SingleAsync(message => message.ProcessedAt == null)).Payload);
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(1, await context.Sessions.CountAsync());
        Assert.Equal(1, await context.RefreshTokens.CountAsync());
        Assert.Equal(0, await Processor(context).ProcessBatchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task BoundedParallelWorkersDeleteEachEligibleRowOnce()
    {
        await Seed();
        await using (var context = database.CreateContext())
        {
            var entry = await context.SecurityAuditEvents.OrderBy(audit => audit.OccurredAt).FirstAsync();
            context.Add(SecurityAuditEvent.Create(SecurityEventType.LoginSuccess, entry.UserId, "native", "trace", new AuditMetadata(), Now.AddDays(-100)));
            await context.SaveChangesAsync();
        }
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var removed = await Task.WhenAll(Processor(first, 1).ProcessBatchAsync(CancellationToken.None), Processor(second, 1).ProcessBatchAsync(CancellationToken.None));
        Assert.Equal(4, removed.Sum());
        await using var after = database.CreateContext();
        Assert.Equal(2, await after.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task LockedRowsAreSkippedAndBecomeEligibleAfterRelease()
    {
        await Seed();
        await using var locked = database.CreateContext();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        await locked.Database.ExecuteSqlRawAsync("SELECT id FROM security_audit_events WHERE occurred_at < '2026-07-21T00:00:00Z' FOR UPDATE");
        await using var other = database.CreateContext();
        Assert.Equal(2, await Processor(other).ProcessBatchAsync(CancellationToken.None));
        await transaction.RollbackAsync();
        Assert.Equal(1, await Processor(other).ProcessBatchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FailureBeforeMaintenanceCommitRollsBackEveryBatch()
    {
        await Seed();
        var options = new DbContextOptionsBuilder<Identity.Infrastructure.Persistence.IdentityDbContext>()
            .UseNpgsql(database.ConnectionString).AddInterceptors(new FailCommit()).Options;
        await using var context = new Identity.Infrastructure.Persistence.IdentityDbContext(options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Processor(context).ProcessBatchAsync(CancellationToken.None));
        await using var after = database.CreateContext();
        Assert.Equal(3, await after.SecurityAuditEvents.CountAsync());
        Assert.Equal(4, await after.OneTimeTokens.CountAsync());
        Assert.Equal(4, await after.OutboxMessages.CountAsync());
    }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class FailCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(System.Data.Common.DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) => throw new InvalidOperationException("injected");
    }
}
