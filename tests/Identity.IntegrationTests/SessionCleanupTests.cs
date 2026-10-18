using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Maintenance;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Identity.IntegrationTests;

public sealed class SessionCleanupTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
    public async Task InitializeAsync()
    {
        await using var context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE users, credentials, sessions, refresh_tokens, one_time_tokens, outbox_messages, security_audit_events CASCADE");
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> SeedFamily(DateTimeOffset created, DateTimeOffset expires, bool revoked = false, int chainLength = 3)
    {
        var user = User.Create($"{Guid.NewGuid():N}@identity.example", null, created);
        user.VerifyEmail(created);
        var session = Session.Create(user.Id, "native", null, created, expires, "identity-native");
        await using var context = database.CreateContext();
        context.AddRange(user, session);
        var previous = RefreshToken.Create(session, Hash(), created, created.AddMinutes(30));
        context.Add(previous);
        for (var index = 1; index < chainLength; index++)
        {
            var now = created.AddSeconds(index);
            var next = RefreshToken.Create(session, Hash(), now, created.AddMinutes(30));
            previous.Consume(next, now);
            context.Add(next);
            previous = next;
        }
        if (revoked) { session.Revoke(SessionRevokeReason.Logout, created.AddMinutes(1)); }
        await context.SaveChangesAsync();
        return session.Id;
    }

    [Fact]
    public async Task CleanupDeletesEntireObsoleteChainsButPreservesHistoryUntilAbsoluteExpiryPlusRetention()
    {
        var removable = await SeedFamily(Now.AddDays(-3), Now.AddDays(-2));
        var retained = await SeedFamily(Now.AddDays(-2), Now.AddHours(-1));
        var active = await SeedFamily(Now.AddHours(-1), Now.AddDays(1));
        var revoked = await SeedFamily(Now.AddHours(-1), Now.AddDays(1), revoked: true);
        await using var context = database.CreateContext();
        var processor = new SessionCleanupProcessor(context, Options.Create(new SessionCleanupOptions()), new FixedClock(Now));
        Assert.Equal(1, await processor.ProcessBatchAsync(CancellationToken.None));
        Assert.Equal(0, await context.Sessions.CountAsync(session => session.Id == removable));
        Assert.Equal(0, await context.RefreshTokens.CountAsync(token => token.SessionId == removable));
        foreach (var id in new[] { retained, active, revoked })
        {
            Assert.Equal(3, await context.RefreshTokens.CountAsync(token => token.SessionId == id));
        }
        Assert.Equal(0, await processor.ProcessBatchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CleanupUsesBoundedFamilyBatches()
    {
        await SeedFamily(Now.AddDays(-4), Now.AddDays(-3));
        await SeedFamily(Now.AddDays(-3), Now.AddDays(-2));
        await using var context = database.CreateContext();
        var processor = new SessionCleanupProcessor(context, Options.Create(new SessionCleanupOptions { BatchSize = 1 }), new FixedClock(Now));
        Assert.Equal(1, await processor.ProcessBatchAsync(CancellationToken.None));
        Assert.Equal(1, await context.Sessions.CountAsync());
        Assert.Equal(3, await context.RefreshTokens.CountAsync());
        Assert.Equal(1, await processor.ProcessBatchAsync(CancellationToken.None));
        Assert.Equal(0, await context.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task ParallelCleanupWorkersNeverDeleteActiveOrPartiallyRetainedFamilies()
    {
        await SeedFamily(Now.AddDays(-3), Now.AddDays(-2));
        await SeedFamily(Now.AddDays(-4), Now.AddDays(-3));
        var active = await SeedFamily(Now.AddHours(-1), Now.AddDays(1));
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var options = Options.Create(new SessionCleanupOptions { BatchSize = 1 });
        var results = await Task.WhenAll(new SessionCleanupProcessor(first, options, new FixedClock(Now)).ProcessBatchAsync(CancellationToken.None),
            new SessionCleanupProcessor(second, options, new FixedClock(Now)).ProcessBatchAsync(CancellationToken.None));
        Assert.Equal(2, results.Sum());
        await using var after = database.CreateContext();
        Assert.Equal(active, (await after.Sessions.SingleAsync()).Id);
        Assert.Equal(3, await after.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task CleanupSkipsLockedFamiliesAndProcessesThemAfterRelease()
    {
        var locked = await SeedFamily(Now.AddDays(-3), Now.AddDays(-2));
        await SeedFamily(Now.AddDays(-4), Now.AddDays(-3));
        await using var blocker = database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Sessions.FromSqlInterpolated($"SELECT *, xmin FROM sessions WHERE id = {locked} FOR UPDATE").SingleAsync();
        await using var worker = database.CreateContext();
        var processor = new SessionCleanupProcessor(worker, Options.Create(new SessionCleanupOptions()), new FixedClock(Now));
        Assert.Equal(1, await processor.ProcessBatchAsync(CancellationToken.None));
        Assert.Equal(0, await processor.ProcessBatchAsync(CancellationToken.None));
        await transaction.RollbackAsync();
        Assert.Equal(1, await processor.ProcessBatchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CleanupRetainsHistoryAtTheExactRetentionBoundary()
    {
        await SeedFamily(Now.AddDays(-2), Now.AddDays(-1));
        await using var context = database.CreateContext();
        Assert.Equal(0, await new SessionCleanupProcessor(context, Options.Create(new SessionCleanupOptions()), new FixedClock(Now))
            .ProcessBatchAsync(CancellationToken.None));
        Assert.Equal(1, await new SessionCleanupProcessor(context, Options.Create(new SessionCleanupOptions()), new FixedClock(Now.AddSeconds(1)))
            .ProcessBatchAsync(CancellationToken.None));
    }

    private static string Hash() => Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    [Fact]
    public async Task CleanupRollsBackFamilyDeletionIfCommitCannotBeReached()
    {
        var id = await SeedFamily(Now.AddDays(-3), Now.AddDays(-2));
        await using var failing = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(database.ConnectionString).AddInterceptors(new SaveFailure()).Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SessionCleanupProcessor(failing,
            Options.Create(new SessionCleanupOptions()), new FixedClock(Now)).ProcessBatchAsync(CancellationToken.None));
        await using var after = database.CreateContext();
        Assert.Equal(id, (await after.Sessions.SingleAsync()).Id);
        Assert.Equal(3, await after.RefreshTokens.CountAsync());
    }

    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected cleanup pre-commit failure.");
    }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
