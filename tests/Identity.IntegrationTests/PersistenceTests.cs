using System.Net;
using System.Net.Http.Json;
using Identity.Api.Contracts;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Application.Features.Health.GetDatabaseReadiness;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Identity.IntegrationTests;

public sealed class PersistenceTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await using var context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE users, credentials, sessions, refresh_tokens, one_time_tokens, outbox_messages, security_audit_events CASCADE");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InitialMigrationCanBeRevertedAndReappliedWithoutModelDrift()
    {
        await using var context = database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(2, applied.Length);
        Assert.EndsWith("_InitialIdentity", applied[0]);
        Assert.EndsWith("_SessionAudienceBinding", applied[1]);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Contains("IF NOT EXISTS", migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent));
        await migrator.MigrateAsync("0");
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());
        var tables = await context.Database.SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'").ToListAsync();
        Assert.Empty(tables);
        await migrator.MigrateAsync();
        Assert.Equal(2, (await context.Database.GetAppliedMigrationsAsync()).Count());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task AudienceMigrationPreservesLegacyDataWithoutGuessingItsOriginalAudience()
    {
        await using var context = database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261018205035_InitialIdentity");
        var user = User.Create("legacy@identity.example", null, Now);
        user.VerifyEmail(Now);
        context.Add(user);
        await context.SaveChangesAsync();
        var id = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var hash = new string('e', 64);
        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO sessions (id, user_id, client_id, created_at, last_used_at, absolute_expires_at) VALUES ({id}, {user.Id}, 'native', {Now}, {Now}, {Now.AddDays(30)})");
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO refresh_tokens (id, session_id, token_hash, created_at, expires_at) VALUES ({tokenId}, {id}, {hash}, {Now}, {Now.AddDays(7)})");
        }
        finally { await migrator.MigrateAsync(); }
        var legacy = await context.Sessions.SingleAsync();
        Assert.Null(legacy.Audience);
        Assert.Equal(id, legacy.Id);
        Assert.Equal(hash, (await context.RefreshTokens.SingleAsync()).TokenHash);
        using var provider = database.CreateProvider();
        using var scope = provider.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ILoginRepository>()
            .IsActiveAsync(user.Id, id, "native", "identity-native", Now, CancellationToken.None));
    }

    [Fact]
    public async Task SchemaContainsAllTablesIndexesAndUtcColumns()
    {
        await using var context = database.CreateContext();
        var tables = await context.Database.SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'").ToListAsync();
        Assert.Equal(new[] { "credentials", "one_time_tokens", "outbox_messages", "refresh_tokens", "security_audit_events", "sessions", "users" }, tables.Order(StringComparer.Ordinal));
        var indexes = await context.Database.SqlQueryRaw<string>("SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'").ToListAsync();
        foreach (var index in new[] { "ux_users_normalized_email", "ux_refresh_tokens_hash", "ux_one_time_tokens_hash",
            "ix_sessions_user_revocation", "ix_sessions_expiry", "ix_refresh_tokens_session", "ix_refresh_tokens_expiry", "ix_outbox_pending" })
        {
            Assert.Contains(index, indexes);
        }
        var naiveTimes = await context.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND data_type = 'timestamp without time zone'").SingleAsync();
        Assert.Equal(0, naiveTimes);
        var columns = await context.Database.SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'").ToListAsync();
        Assert.All(columns, column => Assert.Matches("^[a-z0-9_]+$", column));
    }

    [Fact]
    public async Task AllEntitiesRoundTripAndStateChangesArePersisted()
    {
        await using var context = database.CreateContext();
        var offsetNow = Now.ToOffset(TimeSpan.FromHours(6));
        var user = User.Create("User@Example.com", "Initial", offsetNow);
        var credential = Credential.Create(user.Id, "encoded-hash-fixture", offsetNow);
        var session = Session.Create(user.Id, "native", "Test\r\nAgent", offsetNow, Now.AddDays(30), "identity-native");
        var refresh = RefreshToken.Create(session, new string('a', 64), offsetNow, Now.AddDays(7));
        var oneTime = OneTimeToken.Create(user.Id, OneTimeTokenPurpose.EmailVerification, new string('b', 64), offsetNow, Now.AddHours(24));
        var outbox = OutboxMessage.Create("email_verification", Envelope(), offsetNow);
        var audit = SecurityAuditEvent.Create(SecurityEventType.Register, user.Id, "native", "trace-fixture", new AuditMetadata(1), offsetNow);
        context.AddRange(user, credential, session, refresh, oneTime, outbox, audit);
        Assert.Equal(7, await context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var storedUser = await context.Users.SingleAsync();
        Assert.Equal("USER@EXAMPLE.COM", storedUser.NormalizedEmail);
        Assert.Equal(TimeSpan.Zero, storedUser.CreatedAt.Offset);
        var storedCredential = await context.Credentials.SingleAsync();
        Assert.Equal("encoded-hash-fixture", storedCredential.PasswordHash);
        var storedSession = await context.Sessions.SingleAsync();
        Assert.Equal("TestAgent", storedSession.UserAgent);
        var storedRefresh = await context.RefreshTokens.SingleAsync();
        Assert.Equal(TimeSpan.Zero, storedRefresh.ExpiresAt.Offset);
        var storedOneTime = await context.OneTimeTokens.SingleAsync();
        var storedOutbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal("fixture-key-id", storedOutbox.Payload?.KeyId);
        var storedAudit = await context.SecurityAuditEvents.AsNoTracking().SingleAsync();
        Assert.Equal(1, storedAudit.Metadata.AttemptCount);

        storedUser.VerifyEmail(Now.AddMinutes(1));
        storedUser.SetDisplayName("Changed", Now.AddMinutes(1));
        storedCredential.ChangePasswordHash("updated-encoded-hash-fixture", Now.AddMinutes(1));
        storedSession.Touch(Now.AddMinutes(1));
        storedOneTime.Consume(Now.AddMinutes(1));
        var replacement = RefreshToken.Create(storedSession, new string('c', 64), Now.AddMinutes(1), Now.AddDays(7));
        context.RefreshTokens.Add(replacement);
        storedRefresh.Consume(replacement, Now.AddMinutes(1));
        storedOutbox.MarkProcessed(Now.AddMinutes(1));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(UserStatus.Active, (await context.Users.AsNoTracking().SingleAsync()).Status);
        Assert.Equal("Changed", (await context.Users.AsNoTracking().SingleAsync()).DisplayName);
        Assert.Equal(Now.AddMinutes(1), (await context.Credentials.AsNoTracking().SingleAsync()).PasswordChangedAt);
        Assert.NotNull((await context.OneTimeTokens.AsNoTracking().SingleAsync()).ConsumedAt);
        Assert.Equal(replacement.Id, (await context.RefreshTokens.AsNoTracking().SingleAsync(token => token.Id == refresh.Id)).ReplacedByTokenId);
        Assert.Null((await context.OutboxMessages.AsNoTracking().SingleAsync()).Payload);
    }

    [Fact]
    public async Task LifetimeConstraintsUseElapsedTimeAcrossDaylightSavingChanges()
    {
        await using var context = database.CreateContext();
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("SET TIME ZONE 'America/New_York'");
        try
        {
            var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
            var user = User.Create("dst@example.com", null, now);
            var session = Session.Create(user.Id, "native", null, now, now.AddDays(30), "identity-native");
            var refresh = RefreshToken.Create(session, new string('a', 64), now, now.AddDays(7));
            context.AddRange(user, session, refresh);
            Assert.Equal(3, await context.SaveChangesAsync());
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET TIME ZONE 'UTC'");
        }
    }

    [Fact]
    public async Task RepositoriesShareUnitOfWorkAndSupportCreateReadUpdateDelete()
    {
        await using var provider = database.CreateProvider();
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var credentials = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();
        var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var user = User.Create("repo@example.com", null, Now);
        users.Add(user);
        credentials.Add(Credential.Create(user.Id, "encoded-hash-fixture", Now));
        Assert.Equal(2, await work.SaveChangesAsync(CancellationToken.None));
        Assert.Same(user, await users.FindByIdAsync(user.Id, CancellationToken.None));
        Assert.Same(user, await users.FindByNormalizedEmailAsync(EmailAddress.Normalize(user.Email), CancellationToken.None));
        Assert.NotNull(await credentials.FindByUserIdAsync(user.Id, CancellationToken.None));
        Assert.Null(await users.FindByIdAsync(Guid.NewGuid(), CancellationToken.None));
        user.SetDisplayName("Updated", Now);
        await work.SaveChangesAsync(CancellationToken.None);
        var context = scope.ServiceProvider.GetRequiredService<Identity.Infrastructure.Persistence.IdentityDbContext>();
        context.Credentials.Remove((await credentials.FindByUserIdAsync(user.Id, CancellationToken.None))!);
        context.Users.Remove(user);
        await work.SaveChangesAsync(CancellationToken.None);
        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Users.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SaveChangesRollsBackAllInsertsWhenOneForeignKeyFails()
    {
        await using var context = database.CreateContext();
        context.Users.Add(User.Create("atomic@example.com", null, Now));
        context.Credentials.Add(Credential.Create(Guid.NewGuid(), "encoded-hash-fixture", Now));
        context.OutboxMessages.Add(OutboxMessage.Create("email_verification", Envelope(), Now));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        await using var verification = database.CreateContext();
        Assert.Equal(0, await verification.Users.CountAsync());
        Assert.Equal(0, await verification.OutboxMessages.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitUnitOfWorkTransactionCommitsOrRollsBackMultipleSaves(bool commit)
    {
        await using var provider = database.CreateProvider();
        using (var scope = provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var credentials = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();
            var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await using var transaction = await work.BeginTransactionAsync(CancellationToken.None);
            var user = User.Create("transaction@example.com", null, Now);
            users.Add(user);
            await work.SaveChangesAsync(CancellationToken.None);
            credentials.Add(Credential.Create(user.Id, "encoded-hash-fixture", Now));
            await work.SaveChangesAsync(CancellationToken.None);
            if (commit)
            {
                await transaction.CommitAsync(CancellationToken.None);
            }
            else
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
        }
        await using var verification = database.CreateContext();
        Assert.Equal(commit ? 1 : 0, await verification.Users.CountAsync());
        Assert.Equal(commit ? 1 : 0, await verification.Credentials.CountAsync());
    }

    [Fact]
    public async Task ConcurrentInsertsCannotCreateDuplicateNormalizedEmail()
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Insert(string email)
        {
            await using var context = database.CreateContext();
            context.Users.Add(User.Create(email, null, Now));
            await start.Task;
            try
            {
                await context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return false;
            }
        }
        var first = Insert(" User@Example.com ");
        var second = Insert("user@example.com");
        start.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, result => result);
        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Users.CountAsync());
    }

    [Theory]
    [InlineData("credential")]
    [InlineData("session")]
    [InlineData("one_time")]
    [InlineData("refresh")]
    [InlineData("audit")]
    public async Task ForeignKeysRejectMissingParents(string entity)
    {
        await using var context = database.CreateContext();
        var missingUser = Guid.NewGuid();
        var item = entity switch
        {
            "credential" => (object)Credential.Create(missingUser, "encoded-hash-fixture", Now),
            "session" => Session.Create(missingUser, "native", null, Now, Now.AddDays(30), "identity-native"),
            "one_time" => OneTimeToken.Create(missingUser, OneTimeTokenPurpose.EmailVerification, new string('a', 64), Now, Now.AddHours(24)),
            "refresh" => RefreshToken.Create(Session.Create(missingUser, "native", null, Now, Now.AddDays(30), "identity-native"), new string('b', 64), Now, Now.AddDays(7)),
            _ => SecurityAuditEvent.Create(SecurityEventType.Register, missingUser, null, "trace", new AuditMetadata(), Now)
        };
        context.Add(item);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TokenHashUniqueIndexesAreEnforced(bool refresh)
    {
        await using var context = database.CreateContext();
        var user = User.Create("unique@example.com", null, Now);
        var session = Session.Create(user.Id, "native", null, Now, Now.AddDays(30), "identity-native");
        context.AddRange(user, session);
        await context.SaveChangesAsync();
        if (refresh)
        {
            context.RefreshTokens.Add(RefreshToken.Create(session, new string('a', 64), Now, Now.AddDays(7)));
            context.RefreshTokens.Add(RefreshToken.Create(session, new string('a', 64), Now, Now.AddDays(7)));
        }
        else
        {
            context.OneTimeTokens.Add(OneTimeToken.Create(user.Id, OneTimeTokenPurpose.EmailVerification, new string('a', 64), Now, Now.AddHours(24)));
            context.OneTimeTokens.Add(OneTimeToken.Create(user.Id, OneTimeTokenPurpose.EmailVerification, new string('a', 64), Now, Now.AddHours(24)));
        }
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Theory]
    [InlineData("UPDATE users SET status = 99")]
    [InlineData("UPDATE users SET failed_login_count = -1")]
    [InlineData("UPDATE users SET email = E'user@example.com\\n'")]
    [InlineData("UPDATE sessions SET revoked_at = created_at, revoke_reason = NULL")]
    [InlineData("UPDATE outbox_messages SET payload = NULL")]
    [InlineData("UPDATE outbox_messages SET payload = '{\"token\":\"plaintext-fixture\"}'::jsonb")]
    [InlineData("UPDATE outbox_messages SET payload = jsonb_set(payload, '{tag}', 'null'::jsonb)")]
    [InlineData("UPDATE security_audit_events SET metadata = '{\"password\":\"forbidden-fixture\"}'::jsonb")]
    [InlineData("UPDATE security_audit_events SET metadata = '{\"attemptCount\":\"forbidden-fixture\"}'::jsonb")]
    [InlineData("UPDATE security_audit_events SET metadata = '{\"attemptCount\":-1}'::jsonb")]
    [InlineData("UPDATE security_audit_events SET metadata = '{\"attemptCount\":0.5}'::jsonb")]
    public async Task CheckConstraintsRejectInvalidOrSensitiveState(string sql)
    {
        await using var context = database.CreateContext();
        var user = User.Create("check@example.com", null, Now);
        context.AddRange(user, Session.Create(user.Id, "native", null, Now, Now.AddDays(30), "identity-native"),
            OutboxMessage.Create("email_verification", Envelope(), Now),
            SecurityAuditEvent.Create(SecurityEventType.Register, user.Id, null, "trace", new AuditMetadata(), Now));
        await context.SaveChangesAsync();
        await context.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)context.Database.GetDbConnection());
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task OptimisticConcurrencyPreventsLostLoginCounterUpdates()
    {
        await using var provider = database.CreateProvider();
        Guid id;
        await using (var context = database.CreateContext())
        {
            var user = User.Create("counter@example.com", null, Now);
            context.Users.Add(user);
            await context.SaveChangesAsync();
            id = user.Id;
        }
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var firstUser = await first.ServiceProvider.GetRequiredService<IUserRepository>().FindByIdAsync(id, CancellationToken.None);
        var secondUser = await second.ServiceProvider.GetRequiredService<IUserRepository>().FindByIdAsync(id, CancellationToken.None);
        firstUser!.RecordFailedLogin(Now, null);
        secondUser!.RecordFailedLogin(Now, null);
        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None));
        await using var verification = database.CreateContext();
        Assert.Equal(1, (await verification.Users.AsNoTracking().SingleAsync()).FailedLoginCount);
    }

    [Fact]
    public async Task ConcurrentOneTimeConsumeOnlyCommitsOnce()
    {
        await using var setup = database.CreateContext();
        var user = User.Create("consume@example.com", null, Now);
        var token = OneTimeToken.Create(user.Id, OneTimeTokenPurpose.EmailVerification, new string('a', 64), Now, Now.AddHours(24));
        setup.AddRange(user, token);
        await setup.SaveChangesAsync();
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        (await first.OneTimeTokens.SingleAsync()).Consume(Now.AddMinutes(1));
        (await second.OneTimeTokens.SingleAsync()).Consume(Now.AddMinutes(1));
        var results = await Task.WhenAll(TrySave(first), TrySave(second));
        Assert.Single(results, result => result);
    }

    [Fact]
    public async Task ReplacementForeignKeyCannotLinkDifferentSessions()
    {
        await using var context = database.CreateContext();
        var user = User.Create("family@example.com", null, Now);
        var first = Session.Create(user.Id, "native", null, Now, Now.AddDays(30), "identity-native");
        var second = Session.Create(user.Id, "native", null, Now, Now.AddDays(30), "identity-native");
        var original = RefreshToken.Create(first, new string('a', 64), Now, Now.AddDays(7));
        var foreign = RefreshToken.Create(second, new string('b', 64), Now, Now.AddDays(7));
        context.AddRange(user, first, second, original, foreign);
        await context.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE refresh_tokens SET consumed_at = {Now.AddMinutes(1)}, replaced_by_token_id = {foreign.Id} WHERE id = {original.Id}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task CancelledUnitOfWorkDoesNotPersistChanges()
    {
        await using var provider = database.CreateProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IUserRepository>().Add(User.Create("cancelled@example.com", null, Now));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellation.Token));
        await using var verification = database.CreateContext();
        Assert.Equal(0, await verification.Users.CountAsync());
    }

    [Fact]
    public async Task ConcurrentRefreshRowUpdatesRollBackLosingReplacementInsert()
    {
        await using var setup = database.CreateContext();
        var user = User.Create("refresh-row@example.com", null, Now);
        var session = Session.Create(user.Id, "native", null, Now, Now.AddDays(30), "identity-native");
        var token = RefreshToken.Create(session, new string('a', 64), Now, Now.AddDays(7));
        setup.AddRange(user, session, token);
        await setup.SaveChangesAsync();
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var firstToken = await first.RefreshTokens.SingleAsync();
        var secondToken = await second.RefreshTokens.SingleAsync();
        var firstReplacement = RefreshToken.Create(session, new string('b', 64), Now.AddMinutes(1), Now.AddDays(7));
        var secondReplacement = RefreshToken.Create(session, new string('c', 64), Now.AddMinutes(1), Now.AddDays(7));
        first.RefreshTokens.Add(firstReplacement);
        second.RefreshTokens.Add(secondReplacement);
        firstToken.Consume(firstReplacement, Now.AddMinutes(1));
        secondToken.Consume(secondReplacement, Now.AddMinutes(1));
        var results = await Task.WhenAll(TrySave(first), TrySave(second));
        Assert.Single(results, result => result);
        await using var verification = database.CreateContext();
        Assert.Equal(2, await verification.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task ReadinessQueryAndControllerUseRealPostgreSqlWithoutTrackingEntities()
    {
        await using var provider = database.CreateProvider();
        using (var scope = provider.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
            Assert.True(await dispatcher.Execute<GetDatabaseReadinessQuery, bool>(new GetDatabaseReadinessQuery()));
            var context = scope.ServiceProvider.GetRequiredService<Identity.Infrastructure.Persistence.IdentityDbContext>();
            Assert.Empty(context.ChangeTracker.Entries());
            Assert.Same(TimeProvider.System, scope.ServiceProvider.GetRequiredService<TimeProvider>());
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production").ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = database.ConnectionString })));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ready", (await response.Content.ReadFromJsonAsync<HealthResponse>())?.Status);
    }

    private static EncryptedEmailPayload Envelope() => new("fixture-key-id", Convert.ToBase64String(new byte[12]),
        Convert.ToBase64String(new byte[] { 1, 2, 3 }), Convert.ToBase64String(new byte[16]));

    private static async Task<bool> TrySave(Identity.Infrastructure.Persistence.IdentityDbContext context)
    {
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
