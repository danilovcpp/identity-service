using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Audit.ListAuditEvents;
using Identity.Application.Features.Admin.Common;
using Identity.Application.Features.Admin.Dashboard.GetDashboard;
using Identity.Application.Features.Admin.Users.GetUserDetails;
using Identity.Application.Features.Admin.Users.GetUserSessions;
using Identity.Application.Features.Admin.Users.ListUsers;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Identity.IntegrationTests;

public sealed class AdminReadTests(RegistrationFixture fixture, ITestOutputHelper output) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private const string Email = "reader@identity.example";
    private const string Password = "Walking over the frozen lake 42!";
    private const string Prefix = "/api/v1/admin";
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> Factory(Action<IServiceCollection>? configure = null) => fixture.CreateLoginFactory(new()
    {
        ["AdminAccess:Enabled"] = "true",
        ["AdminAccess:DataProtectionKeyPath"] = fixture.AdminKeyDirectory
    }, services =>
    {
        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FrozenClock()));
        configure?.Invoke(services);
    });

    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task<Guid> SeedOperator(WebApplicationFactory<Program> factory, AdministratorRole role = AdministratorRole.IdentityReadOnly)
    {
        using var scope = factory.Services.CreateScope();
        var user = NewUser(Email, UserStatus.Active, Now.AddDays(-40));
        await using var context = fixture.CreateContext();
        context.AddRange(user, Credential.Create(user.Id, scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(Password), user.CreatedAt),
            IdentityAdministrator.Grant(user, role, null, user.CreatedAt));
        await context.SaveChangesAsync();
        return user.Id;
    }

    private static async Task Login(HttpClient client)
    {
        using var csrf = await client.GetAsync(Prefix + "/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrf.StatusCode);
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Post, Prefix + "/auth/login")
        { Content = JsonContent.Create(new { email = Email, password = Password }) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static User NewUser(string email, UserStatus status, DateTimeOffset created, string? name = null)
    {
        var user = status == UserStatus.Invited ? User.CreateInvited(email, name, created) : User.Create(email, name, created);
        if (status is UserStatus.Active or UserStatus.Disabled or UserStatus.Deactivated) { user.VerifyEmail(created); }
        if (status == UserStatus.Disabled) { user.Disable(created); }
        if (status == UserStatus.Deactivated) { user.Deactivate(created); }
        return user;
    }

    private async Task<T> Query<TQuery, T>(WebApplicationFactory<Program> factory, TQuery query) where TQuery : IQuery<T>
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>().Execute<TQuery, T>(query);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<IdentityDbContext>().ChangeTracker.Entries());
        return result;
    }

    [Fact]
    public async Task EmptyDatabaseReturnsZeroCountsThirtyUtcDaysAndEmptyPages()
    {
        await using var factory = Factory();
        var dashboard = await Query<GetDashboardQuery, DashboardResult>(factory, new());
        Assert.Equal(0, dashboard.TotalUsers);
        Assert.Equal(0, dashboard.ActiveSessions);
        Assert.Equal(30, dashboard.RegistrationTrend.Count);
        Assert.All(dashboard.RegistrationTrend, point => Assert.Equal(0, point.Count));
        Assert.Equal(Now, dashboard.AsOf);
        Assert.Equal(new DateOnly(2026, 9, 20), dashboard.RegistrationTrend[0].Date);
        Assert.Equal(new DateOnly(2026, 10, 19), dashboard.RegistrationTrend[^1].Date);
        Assert.Equal(TimeSpan.Zero, dashboard.RegistrationTrendFrom.Offset);
        Assert.Equal(Now.Date, dashboard.RegistrationTrendTo.AddDays(-1).Date);
        Assert.Empty((await Query<ListUsersQuery, PageResult<AdminUserListItem>>(factory, new())).Items);
        Assert.Empty((await Query<ListAuditEventsQuery, PageResult<AdminAuditEvent>>(factory, new())).Items);
    }

    [Fact]
    public async Task DashboardCountsAllStatusesAndUsesUtcDaysRegardlessOfDatabaseTimezone()
    {
        await using var factory = Factory();
        await using (var context = fixture.CreateContext())
        {
            var users = Enum.GetValues<UserStatus>().Select((status, index) => NewUser($"state{index}@identity.example", status,
                index == 0 ? new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero) : Now.AddMinutes(-10))).ToArray();
            var before = NewUser("before@identity.example", UserStatus.Active, new DateTimeOffset(2026, 9, 19, 23, 59, 59, TimeSpan.Zero));
            var midnight = NewUser("midnight@identity.example", UserStatus.Active, new DateTimeOffset(2026, 10, 18, 20, 30, 0, TimeSpan.FromHours(-4)));
            var future = NewUser("future@identity.example", UserStatus.Invited, Now.AddMinutes(1));
            context.AddRange(users);
            context.AddRange(before, midnight, future);
            var active = Session.Create(users[1].Id, "native", null, Now.AddHours(-1), Now.AddHours(1), "identity-native");
            var revoked = Session.Create(users[1].Id, "native", null, Now.AddHours(-1), Now.AddHours(1), "identity-native");
            revoked.Revoke(SessionRevokeReason.Logout, Now.AddMinutes(-1));
            var expired = Session.Create(users[1].Id, "native", null, Now.AddHours(-1), Now, "identity-native");
            context.AddRange(active, revoked, expired, AdminSession.Create(users[1].Id, Now, Now.AddHours(1)));
            await context.SaveChangesAsync();
        }
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("SET TIME ZONE 'America/New_York'");
        try
        {
            var dashboard = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>().Execute<GetDashboardQuery, DashboardResult>(new());
            Assert.Equal(8, dashboard.TotalUsers);
            Assert.Equal(3, dashboard.ActiveUsers);
            Assert.Equal(1, dashboard.PendingVerificationUsers);
            Assert.Equal(2, dashboard.InvitedUsers);
            Assert.Equal(1, dashboard.DisabledUsers);
            Assert.Equal(1, dashboard.DeactivatedUsers);
            Assert.Equal(1, dashboard.ActiveSessions);
            Assert.Equal(6, dashboard.RegistrationTrend.Sum(point => point.Count));
            Assert.Equal(1, dashboard.RegistrationTrend[0].Count);
            Assert.Equal(5, dashboard.RegistrationTrend[^1].Count);
        }
        finally { await db.Database.ExecuteSqlRawAsync("SET TIME ZONE 'UTC'"); }
    }

    [Theory]
    [InlineData(AdministratorRole.IdentityAdministrator)]
    [InlineData(AdministratorRole.IdentityReadOnly)]
    public async Task BothRolesReadAllFiveEndpointsWithSafeResponsesAndOpenApi(AdministratorRole role)
    {
        var logs = new SecretLogCapture();
        await using var factory = Factory(services => services.AddLogging(logging => logging.AddProvider(logs)));
        var operatorId = await SeedOperator(factory, role);
        var target = NewUser("target@identity.example", UserStatus.Disabled, Now.AddMinutes(-1), "Target");
        await using (var context = fixture.CreateContext())
        {
            context.AddRange(target, Credential.Create(target.Id, "encoded-secret-hash-sentinel", target.CreatedAt),
                Session.Create(target.Id, "native", "Test Agent", target.CreatedAt, Now.AddDays(1), "identity-native"));
            await context.SaveChangesAsync();
        }
        using var client = Client(factory);
        await Login(client);
        foreach (var path in new[] { "/dashboard", "/users", $"/users/{target.Id}", $"/users/{target.Id}/sessions", "/audit-events" })
        {
            using var response = await client.GetAsync(Prefix + path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            var json = await response.Content.ReadAsStringAsync();
            foreach (var secret in new[] { Password, "encoded-secret-hash-sentinel", "passwordHash", "tokenHash", "refreshToken", "normalizedEmail" })
            { Assert.DoesNotContain(secret, json); }
        }
        var details = await client.GetFromJsonAsync<AdminUserDetails>(Prefix + $"/users/{target.Id}");
        Assert.Equal("Disabled", details!.Status);
        Assert.Equal(target.Id, details.Id);
        Assert.Equal(target.CreatedAt, details.DisabledAt);
        // Stage 10 now exposes this route; unsafe calls require CSRF and the full write role.
        using var write = await client.PostAsJsonAsync(Prefix + $"/users/{target.Id}/disable", new { reason = "Read test" });
        Assert.Equal(role == AdministratorRole.IdentityReadOnly ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest, write.StatusCode);
        using var api = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = api.RootElement.GetProperty("paths");
        foreach (var path in new[] { "/dashboard", "/users", "/users/{userId}", "/users/{userId}/sessions", "/audit-events" })
        { Assert.True(paths.GetProperty(Prefix + path).TryGetProperty("get", out _)); }
        Assert.DoesNotContain(logs.Lines, line => line.Contains(Password, StringComparison.Ordinal)
            || line.Contains("target@identity.example", StringComparison.Ordinal) || line.Contains("encoded-secret-hash-sentinel", StringComparison.Ordinal));
        await using var verify = fixture.CreateContext();
        Assert.Equal(2, await verify.Users.CountAsync());
        Assert.Equal(UserStatus.Disabled, (await verify.Users.SingleAsync(user => user.Id == target.Id)).Status);
        Assert.Equal(1, await verify.AdminSessions.CountAsync(session => session.UserId == operatorId));
    }

    [Theory]
    [InlineData("createdAt", "asc")]
    [InlineData("createdAt", "desc")]
    [InlineData("email", "asc")]
    [InlineData("email", "desc")]
    [InlineData("status", "asc")]
    [InlineData("status", "desc")]
    public async Task UserPaginationHasStableTiesAndSupportsEveryWhitelistedSort(string sort, string direction)
    {
        await using var factory = Factory();
        var users = Enumerable.Range(0, 27).Select(index => NewUser($"user{index:00}@identity.example",
            index % 2 == 0 ? UserStatus.Active : UserStatus.Invited, Now.AddDays(-1))).ToArray();
        await using (var context = fixture.CreateContext()) { context.AddRange(users); await context.SaveChangesAsync(); }
        var ids = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await Query<ListUsersQuery, PageResult<AdminUserListItem>>(factory,
                new ListUsersQuery { Page = page, PageSize = 10, SortBy = sort, SortDirection = direction });
            Assert.Equal(27, result.Total);
            Assert.Equal(page == 3 ? 7 : 10, result.Items.Count);
            ids.AddRange(result.Items.Select(user => user.Id));
        }
        Assert.Equal(27, ids.Distinct().Count());
        await using var db = fixture.CreateContext();
        var ordered = (sort, direction) switch
        {
            ("createdAt", "asc") => db.Users.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id),
            ("createdAt", _) => db.Users.OrderByDescending(user => user.CreatedAt).ThenByDescending(user => user.Id),
            ("email", "asc") => db.Users.OrderBy(user => user.Email).ThenBy(user => user.Id),
            ("email", _) => db.Users.OrderByDescending(user => user.Email).ThenByDescending(user => user.Id),
            (_, "asc") => db.Users.OrderBy(user => user.Status).ThenBy(user => user.Id),
            _ => db.Users.OrderByDescending(user => user.Status).ThenByDescending(user => user.Id)
        };
        Assert.Equal(await ordered.Select(user => user.Id).ToArrayAsync(), ids);
        var beyond = await Query<ListUsersQuery, PageResult<AdminUserListItem>>(factory, new ListUsersQuery { Page = 4, PageSize = 10 });
        Assert.Empty(beyond.Items);
        Assert.Equal(27, beyond.Total);
    }

    [Fact]
    public async Task SearchIsCaseInsensitiveLiteralAndCombinesStatusAndVerificationFilters()
    {
        await using var factory = Factory();
        var match = NewUser("literal@identity.example", UserStatus.Invited, Now, "Mixed Name_%\\");
        await using (var context = fixture.CreateContext())
        {
            context.AddRange(match, NewUser("other@identity.example", UserStatus.Active, Now, "Mixed NameABC"));
            await context.SaveChangesAsync();
        }
        foreach (var search in new[] { "mIxEd", "literal@IDENTITY", "_%\\" })
        {
            var result = await Query<ListUsersQuery, PageResult<AdminUserListItem>>(factory,
                new ListUsersQuery { Search = search, Status = "Invited", EmailVerified = false });
            Assert.Equal(match.Id, Assert.Single(result.Items).Id);
        }
        var noMatch = await Query<ListUsersQuery, PageResult<AdminUserListItem>>(factory, new ListUsersQuery { Search = "' OR 1=1 --" });
        Assert.Empty(noMatch.Items);
        Assert.Empty((await Query<ListUsersQuery, PageResult<AdminUserListItem>>(factory,
            new ListUsersQuery { Status = "Invited", EmailVerified = true })).Items);
    }

    [Fact]
    public async Task SessionsAreOwnedPaginatedAndUseStrictExpiryAndRevocationBoundaries()
    {
        await using var factory = Factory();
        var owner = NewUser("owner@identity.example", UserStatus.Active, Now.AddDays(-1));
        var foreign = NewUser("foreign@identity.example", UserStatus.Active, Now.AddDays(-1));
        var active = Session.Create(owner.Id, "native", "Safe Agent", Now.AddHours(-1), Now.AddHours(1), "identity-native");
        var revoked = Session.Create(owner.Id, "native", null, Now.AddHours(-1), Now.AddHours(1), "identity-native");
        revoked.Revoke(SessionRevokeReason.UserRevoked, Now);
        var expired = Session.Create(owner.Id, "native", null, Now.AddHours(-1), Now, "identity-native");
        await using (var context = fixture.CreateContext())
        {
            context.AddRange(owner, foreign, active, revoked, expired,
                Session.Create(foreign.Id, "native", null, Now.AddHours(-1), Now.AddHours(1), "identity-native"));
            await context.SaveChangesAsync();
        }
        var all = await Query<GetUserSessionsQuery, PageResult<AdminUserSession>>(factory, new(owner.Id, pageSize: 2));
        Assert.Equal(3, all.Total);
        Assert.Equal(2, all.Items.Count);
        foreach (var state in new[] { "active", "revoked", "expired" })
        {
            var result = await Query<GetUserSessionsQuery, PageResult<AdminUserSession>>(factory, new(owner.Id, state: state));
            var item = Assert.Single(result.Items);
            Assert.Equal(state == "active" ? active.Id : state == "revoked" ? revoked.Id : expired.Id, item.Id);
            Assert.Equal(state == "active", item.IsActive);
        }
        Assert.Empty((await Query<GetUserSessionsQuery, PageResult<AdminUserSession>>(factory, new(owner.Id, page: 5))).Items);
    }

    [Fact]
    public async Task AuditFiltersSupportLegacySubjectsAdminActorsTargetsAndHalfOpenOffsetRanges()
    {
        await using var factory = Factory();
        var actor = NewUser("actor@identity.example", UserStatus.Active, Now);
        var target = NewUser("target@identity.example", UserStatus.Invited, Now);
        var legacy = SecurityAuditEvent.Create(SecurityEventType.LoginSuccess, target.Id, "native", "legacy", new AuditMetadata(), Now);
        var admin = SecurityAuditEvent.Create(SecurityEventType.AdminInvitedUser, null, null, "admin", new AuditMetadata(), Now, actor.Id, target.Id);
        await using (var context = fixture.CreateContext())
        {
            context.AddRange(actor, target, legacy, admin,
                SecurityAuditEvent.Create(SecurityEventType.AdminInvitedUser, null, null, "end", new AuditMetadata(), Now.AddSeconds(1), actor.Id, target.Id),
                SecurityAuditEvent.Create(SecurityEventType.LoginFailed, null, null, "unknown", new AuditMetadata(), Now));
            await context.SaveChangesAsync();
        }
        var result = await Query<ListAuditEventsQuery, PageResult<AdminAuditEvent>>(factory, new()
        { UserId = target.Id, ActorUserId = actor.Id, EventType = "admin_invited_user", From = "2026-10-19T18:00:00+06:00", To = "2026-10-19T12:00:01Z" });
        Assert.Equal(admin.Id, Assert.Single(result.Items).Id);
        Assert.Equal(3, (await Query<ListAuditEventsQuery, PageResult<AdminAuditEvent>>(factory, new() { UserId = target.Id })).Total);
        var first = await Query<ListAuditEventsQuery, PageResult<AdminAuditEvent>>(factory, new() { PageSize = 2 });
        var second = await Query<ListAuditEventsQuery, PageResult<AdminAuditEvent>>(factory, new() { PageSize = 2, Page = 2 });
        Assert.Equal(4, first.Total);
        Assert.Equal(4, first.Items.Concat(second.Items).Select(item => item.Id).Distinct().Count());
        Assert.Empty((await Query<ListAuditEventsQuery, PageResult<AdminAuditEvent>>(factory, new() { EventType = "unknown_event" })).Items);
    }

    [Theory]
    [InlineData("/users?page=0")]
    [InlineData("/users?pageSize=101")]
    [InlineData("/users?page=2147483647&pageSize=100")]
    [InlineData("/users?status=1")]
    [InlineData("/users?sortBy=DROP_TABLE")]
    [InlineData("/users?emailVerified=not-bool")]
    [InlineData("/audit-events?from=2026-10-19T12:00:00")]
    [InlineData("/audit-events?from=2026-10-20T12:00:00Z&to=2026-10-19T12:00:00Z")]
    [InlineData("/audit-events?actorUserId=00000000-0000-0000-0000-000000000000")]
    public async Task InvalidReadRequestsUseUnifiedProblemDetailsWithoutReflectingInput(string path)
    {
        await using var factory = Factory();
        await SeedOperator(factory);
        using var client = Client(factory);
        await Login(client);
        using var response = await client.GetAsync(Prefix + path);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", json.GetProperty("code").GetString());
        Assert.True(json.TryGetProperty("traceId", out _));
        Assert.True(json.TryGetProperty("errors", out _));
        Assert.DoesNotContain("DROP_TABLE", json.ToString());
    }

    [Fact]
    public async Task ReadRoutesDenyAnonymousBearerAndRevokedCookieAndReturn404ForAbsentUsers()
    {
        await using var factory = Factory();
        await SeedOperator(factory);
        using var client = Client(factory);
        var missingId = Guid.NewGuid();
        var paths = new[] { "/dashboard", "/users", $"/users/{missingId}", $"/users/{missingId}/sessions", "/audit-events" };
        foreach (var path in paths)
        {
            using var anonymous = await client.GetAsync(Prefix + path);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            Assert.Null(anonymous.Headers.Location);
        }
        using var native = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password, clientId = "native" });
        var token = (await native.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var bearer = await client.GetAsync(Prefix + "/users");
        Assert.Equal(HttpStatusCode.Unauthorized, bearer.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        await Login(client);
        foreach (var path in paths.Skip(2).Take(2))
        {
            using var missing = await client.GetAsync(Prefix + path);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal("user_not_found", (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        await using (var context = fixture.CreateContext())
        { await context.AdminSessions.ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, Now)); }
        using var revoked = await client.GetAsync(Prefix + "/audit-events");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [Fact]
    public async Task QueryCountIsConstantAndProjectionsStayUntrackedOnLargerDataset()
    {
        var recorder = new SqlRecorder();
        await using var factory = Factory(services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(recorder)));
        await using (var context = fixture.CreateContext())
        { context.AddRange(Enumerable.Range(0, 5000).Select(index => NewUser($"scale{index:0000}@identity.example", UserStatus.Active, Now.AddDays(-1)))); await context.SaveChangesAsync(); }
        var watch = Stopwatch.StartNew();
        recorder.Commands.Clear();
        var page = await Query<ListUsersQuery, PageResult<AdminUserListItem>>(factory, new() { PageSize = 100 });
        Assert.Equal(5000, page.Total);
        Assert.Equal(100, page.Items.Count);
        Assert.Equal(2, recorder.Commands.Count);
        Assert.Contains(recorder.Commands, sql => sql.Contains("LIMIT", StringComparison.Ordinal) && sql.Contains("OFFSET", StringComparison.Ordinal));
        recorder.Commands.Clear();
        await Query<GetUserDetailsQuery, AdminUserDetails>(factory, new(page.Items[0].Id));
        Assert.Single(recorder.Commands);
        recorder.Commands.Clear();
        await Query<GetUserSessionsQuery, PageResult<AdminUserSession>>(factory, new(page.Items[0].Id));
        Assert.Equal(3, recorder.Commands.Count);
        recorder.Commands.Clear();
        await Query<ListAuditEventsQuery, PageResult<AdminAuditEvent>>(factory, new());
        Assert.Equal(2, recorder.Commands.Count);
        recorder.Commands.Clear();
        var dashboard = await Query<GetDashboardQuery, DashboardResult>(factory, new());
        Assert.Equal(5000, dashboard.TotalUsers);
        Assert.Equal(3, recorder.Commands.Count);
        Assert.Contains(recorder.Commands, sql => sql.Contains("GROUP BY", StringComparison.Ordinal) && sql.Contains("AT TIME ZONE 'UTC'", StringComparison.Ordinal));
        Assert.DoesNotContain(recorder.Commands, sql => sql.Contains("credentials", StringComparison.Ordinal) || sql.Contains("password_hash", StringComparison.Ordinal));
        output.WriteLine("Five admin read queries over 5000 users: {0:F1} ms including local DB/DI; fixed 2/1/3/2/3 SELECT counts, not a production SLA.", watch.Elapsed.TotalMilliseconds);
    }

    private sealed class FrozenClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    [Fact]
    public async Task CountAndPageUseOneSnapshotWhenAnotherConnectionInsertsBetweenSelects()
    {
        var race = new SnapshotRace(async () =>
        {
            await using var writer = fixture.CreateContext();
            writer.Add(NewUser("concurrent@identity.example", UserStatus.Active, Now));
            await writer.SaveChangesAsync();
        });
        await using var factory = Factory(services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(race)));
        await using (var seed = fixture.CreateContext())
        { seed.Add(NewUser("original@identity.example", UserStatus.Active, Now)); await seed.SaveChangesAsync(); }
        var page = await Query<ListUsersQuery, PageResult<AdminUserListItem>>(factory, new());
        Assert.True(race.Called);
        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        await using var after = fixture.CreateContext();
        Assert.Equal(2, await after.Users.CountAsync());
    }

    private sealed class SnapshotRace(Func<Task> write) : DbCommandInterceptor
    {
        public bool Called { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Called && command.CommandText.Contains("FROM users", StringComparison.Ordinal) && command.CommandText.Contains("LIMIT", StringComparison.Ordinal))
            { Called = true; await write(); }
            return result;
        }
    }

    private sealed class SqlRecorder : DbCommandInterceptor
    {
        public ConcurrentBag<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
