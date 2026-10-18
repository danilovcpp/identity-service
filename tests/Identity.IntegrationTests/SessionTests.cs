using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit.Abstractions;

namespace Identity.IntegrationTests;

public sealed class SessionTests(RegistrationFixture fixture, ITestOutputHelper output) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    private const string Password = "Walking over the frozen lake 42!";
    private const string Email = "sessions@identity.example";
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> SeedAsync(WebApplicationFactory<Program> factory, string email = Email)
    {
        using var scope = factory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().AddMinutes(-1);
        var user = User.Create(email, "Session Test", now);
        user.VerifyEmail(now);
        await using var context = fixture.CreateContext();
        context.Add(user);
        context.Add(Credential.Create(user.Id, scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(Password), now));
        await context.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<TokenPair> Login(HttpClient client, string email = Email, string clientId = "native")
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password, clientId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TokenPair>())!;
    }

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string? token) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = token });

    private static Task<HttpResponseMessage> Logout(HttpClient client, string? token) =>
        client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = token });

    private static async Task<HttpResponseMessage> Authorized(HttpClient client, TokenPair pair, HttpMethod method, string path = "/api/v1/sessions")
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task SequentialRotationPreservesFamilyAndAudienceAndStoresOnlyHashes()
    {
        var logs = new SecretLogCapture();
        await using var factory = fixture.CreateLoginFactory(configure: services => services.AddLogging(logging => logging.AddProvider(logs)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var original = await Login(client);
        var current = original;
        var issued = new List<TokenPair> { original };
        var watch = Stopwatch.StartNew();
        for (var index = 0; index < 5; index++)
        {
            using var response = await Refresh(client, current.RefreshToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var next = (await response.Content.ReadFromJsonAsync<TokenPair>())!;
            Assert.Equal(900, next.ExpiresIn);
            Assert.NotEqual(current.RefreshToken, next.RefreshToken);
            Assert.NotEqual(current.AccessToken, next.AccessToken);
            var jwt = new JsonWebToken(next.AccessToken);
            Assert.Equal("native", jwt.GetClaim("client_id").Value);
            Assert.Equal("identity-native", Assert.Single(jwt.Audiences));
            Assert.Equal(new JsonWebToken(original.AccessToken).GetClaim("sid").Value, jwt.GetClaim("sid").Value);
            current = next;
            issued.Add(next);
        }
        watch.Stop();
        output.WriteLine("5 sequential refresh operations: {0:F1} ms total, {1:F1} ms mean (local sample, not production SLA).", watch.Elapsed.TotalMilliseconds, watch.Elapsed.TotalMilliseconds / 5);
        await using var context = fixture.CreateContext();
        var session = await context.Sessions.SingleAsync();
        Assert.Equal("identity-native", session.Audience);
        var chain = await context.RefreshTokens.ToListAsync();
        Assert.Equal(6, chain.Count);
        Assert.Equal(5, chain.Count(token => token.ConsumedAt is not null && token.ReplacedByTokenId is not null));
        Assert.All(chain, token =>
        {
            Assert.Equal(session.Id, token.SessionId);
            Assert.Null(token.RevokedAt);
            Assert.Equal(64, token.TokenHash.Length);
            Assert.True(token.ExpiresAt <= session.AbsoluteExpiresAt);
        });
        Assert.Equal(factory.Services.GetRequiredService<ITokenGenerator>().Hash(current.RefreshToken), chain.Single(token => token.ConsumedAt is null).TokenHash);
        Assert.Equal(5, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "refresh_rotated"));
        using var sessions = await Authorized(client, current, HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, sessions.StatusCode);
        Assert.DoesNotContain(logs.Lines, line => line.Contains(Password, StringComparison.Ordinal)
            || issued.Any(pair => line.Contains(pair.AccessToken, StringComparison.Ordinal) || line.Contains(pair.RefreshToken, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CleanupWorkerRemovesObsoleteFamiliesInTheRunningApi()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create("cleanup@identity.example", null, now.AddDays(-3));
        user.VerifyEmail(now.AddDays(-3));
        var session = Session.Create(user.Id, "native", null, now.AddDays(-3), now.AddDays(-2), "identity-native");
        await using (var context = fixture.CreateContext())
        {
            context.AddRange(user, session, RefreshToken.Create(session, new string('f', 64), now.AddDays(-3), now.AddDays(-3).AddHours(1)));
            await context.SaveChangesAsync();
        }
        await using var factory = fixture.CreateLoginFactory(new() { ["SessionCleanup:Enabled"] = "true", ["SessionCleanup:PollSeconds"] = "1" });
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var context = fixture.CreateContext();
            if (!await context.Sessions.AnyAsync())
            {
                Assert.Equal(0, await context.RefreshTokens.CountAsync());
                return;
            }
            await Task.Delay(100);
        }
        Assert.Fail("The cleanup worker did not remove the obsolete family.");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task ConcurrentRefreshRotatesOnceAndReplayRevokesTheWinningToken(int requests)
    {
        await using var factory = fixture.CreateLoginFactory();
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var original = await Login(client);
        var responses = await Task.WhenAll(Enumerable.Range(0, requests).Select(_ => Refresh(client, original.RefreshToken)));
        TokenPair winner;
        try
        {
            var success = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Equal(requests - 1, responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized));
            winner = (await success.Content.ReadFromJsonAsync<TokenPair>())!;
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        using var rejected = await Refresh(client, winner.RefreshToken);
        await AssertInvalidRefresh(rejected);
        using var access = await Authorized(client, winner, HttpMethod.Get);
        Assert.Equal(HttpStatusCode.Unauthorized, access.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(SessionRevokeReason.RefreshReplay, (await context.Sessions.SingleAsync()).RevokeReason);
        var tokens = await context.RefreshTokens.ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, token => Assert.NotNull(token.RevokedAt));
        Assert.Equal(1, tokens.Count(token => token.ConsumedAt is not null));
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "refresh_rotated"));
        Assert.Equal(requests - 1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "refresh_replay_detected"));
    }

    [Fact]
    public async Task ReplayOfAnExpiredConsumedTokenStillRevokesItsLiveReplacement()
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var factory = fixture.CreateLoginFactory(configure: services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var original = await Login(client);
        clock.Advance(TimeSpan.FromDays(1));
        using var response = await Refresh(client, original.RefreshToken);
        var current = (await response.Content.ReadFromJsonAsync<TokenPair>())!;
        clock.Advance(TimeSpan.FromDays(6).Add(TimeSpan.FromSeconds(1)));
        using var replay = await Refresh(client, original.RefreshToken);
        await AssertInvalidRefresh(replay);
        using var revoked = await Refresh(client, current.RefreshToken);
        await AssertInvalidRefresh(revoked);
        await using var context = fixture.CreateContext();
        Assert.Equal(SessionRevokeReason.RefreshReplay, (await context.Sessions.SingleAsync()).RevokeReason);
    }

    [Fact]
    public async Task RefreshClipsExpiryToAbsoluteSessionBoundaryAndCannotExtendIt()
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var factory = fixture.CreateLoginFactory(new() { ["Login:SessionLifetimeSeconds"] = "120" },
            services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var original = await Login(client);
        clock.Advance(TimeSpan.FromSeconds(90));
        using var rotated = await Refresh(client, original.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var next = (await rotated.Content.ReadFromJsonAsync<TokenPair>())!;
        await using (var context = fixture.CreateContext())
        {
            var session = await context.Sessions.SingleAsync();
            Assert.Equal(TimeSpan.FromSeconds(120), session.AbsoluteExpiresAt - session.CreatedAt);
            var latest = await context.RefreshTokens.SingleAsync(token => token.ConsumedAt == null);
            Assert.Equal(session.AbsoluteExpiresAt, latest.ExpiresAt);
            Assert.Equal(TimeSpan.FromSeconds(30), latest.ExpiresAt - latest.CreatedAt);
            Assert.Equal(clock.GetUtcNow().ToUnixTimeSeconds(), session.LastUsedAt.ToUnixTimeSeconds());
        }
        clock.Advance(TimeSpan.FromSeconds(30));
        using var expired = await Refresh(client, next.RefreshToken);
        await AssertInvalidRefresh(expired);
    }

    [Theory]
    [InlineData("expired-token")]
    [InlineData("disabled-user")]
    [InlineData("deactivated-user")]
    [InlineData("locked-user")]
    [InlineData("revoked-token")]
    [InlineData("unbound-audience")]
    public async Task InvalidStatesNeverIssueReplacementTokens(string scenario)
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var factory = fixture.CreateLoginFactory(configure: services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        await using (var context = fixture.CreateContext())
        {
            var user = await context.Users.SingleAsync();
            if (scenario == "disabled-user") { user.Disable(clock.GetUtcNow()); }
            if (scenario == "deactivated-user") { user.Deactivate(clock.GetUtcNow()); }
            if (scenario == "locked-user") { user.RecordFailedLogin(clock.GetUtcNow(), clock.GetUtcNow().AddMinutes(15)); }
            if (scenario == "revoked-token") { (await context.RefreshTokens.SingleAsync()).Revoke(clock.GetUtcNow()); }
            await context.SaveChangesAsync();
            if (scenario == "unbound-audience") { await context.Database.ExecuteSqlRawAsync("UPDATE sessions SET audience = NULL"); }
        }
        if (scenario == "expired-token") { clock.Advance(TimeSpan.FromDays(7)); }
        using var response = await Refresh(client, pair.RefreshToken);
        await AssertInvalidRefresh(response);
        await using var after = fixture.CreateContext();
        Assert.Equal(1, await after.RefreshTokens.CountAsync());
        Assert.Null((await after.RefreshTokens.SingleAsync()).ConsumedAt);
        Assert.Equal(0, await after.SecurityAuditEvents.CountAsync(audit => audit.EventType == "refresh_rotated"));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("audience-changed")]
    public async Task RestartCannotSwitchAudienceOrEnableRefreshForDisabledClients(string scenario)
    {
        TokenPair pair;
        await using (var first = fixture.CreateLoginFactory())
        {
            await SeedAsync(first);
            using var client = first.CreateClient();
            pair = await Login(client);
        }
        await using var changed = fixture.CreateLoginFactory(new()
        {
            [scenario == "disabled" ? "Identity:Clients:0:Enabled" : "Identity:Clients:0:Audience"] =
            scenario == "disabled" ? "false" : "different-audience"
        });
        using var nextClient = changed.CreateClient();
        using var response = await Refresh(nextClient, pair.RefreshToken);
        await AssertInvalidRefresh(response);
        await using var context = fixture.CreateContext();
        Assert.Equal("identity-native", (await context.Sessions.SingleAsync()).Audience);
        Assert.Equal(1, await context.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task AdditionalClientFieldsCannotChangeTheBoundAudience()
    {
        await using var factory = fixture.CreateLoginFactory();
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        using var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { pair.RefreshToken, clientId = "backend", audience = "identity-backend" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var next = (await response.Content.ReadFromJsonAsync<TokenPair>())!;
        Assert.Equal("native", new JsonWebToken(next.AccessToken).GetClaim("client_id").Value);
        Assert.Equal("identity-native", Assert.Single(new JsonWebToken(next.AccessToken).Audiences));
    }

    [Fact]
    public async Task RefreshStateSurvivesRestartAndOldTokenRemainsReplayEvidence()
    {
        TokenPair original;
        TokenPair rotated;
        await using (var first = fixture.CreateLoginFactory())
        {
            await SeedAsync(first);
            using var client = first.CreateClient();
            original = await Login(client);
            using var response = await Refresh(client, original.RefreshToken);
            rotated = (await response.Content.ReadFromJsonAsync<TokenPair>())!;
        }
        await using var restarted = fixture.CreateLoginFactory();
        using var nextClient = restarted.CreateClient();
        using var nextResponse = await Refresh(nextClient, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
        var next = (await nextResponse.Content.ReadFromJsonAsync<TokenPair>())!;
        using var replay = await Refresh(nextClient, original.RefreshToken);
        await AssertInvalidRefresh(replay);
        using var revoked = await Refresh(nextClient, next.RefreshToken);
        await AssertInvalidRefresh(revoked);
        await using var context = fixture.CreateContext();
        Assert.Equal(3, await context.RefreshTokens.CountAsync());
        Assert.All(await context.RefreshTokens.ToListAsync(), token => Assert.NotNull(token.RevokedAt));
    }

    [Theory]
    [InlineData("current")]
    [InlineData("consumed")]
    public async Task LogoutIsIdempotentAndRevokesTheWholeFamilyWithoutIssuingTokens(string presented)
    {
        await using var factory = fixture.CreateLoginFactory();
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var original = await Login(client);
        using var rotation = await Refresh(client, original.RefreshToken);
        var current = (await rotation.Content.ReadFromJsonAsync<TokenPair>())!;
        var token = presented == "consumed" ? original.RefreshToken : current.RefreshToken;
        using var logout = await Logout(client, token);
        using var repeated = await Logout(client, token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.Empty(await logout.Content.ReadAsStringAsync());
        using var refresh = await Refresh(client, current.RefreshToken);
        await AssertInvalidRefresh(refresh);
        using var access = await Authorized(client, current, HttpMethod.Get);
        Assert.Equal(HttpStatusCode.Unauthorized, access.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(SessionRevokeReason.Logout, (await context.Sessions.SingleAsync()).RevokeReason);
        Assert.Equal(2, await context.RefreshTokens.CountAsync());
        Assert.All(await context.RefreshTokens.ToListAsync(), row => Assert.NotNull(row.RevokedAt));
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "logout"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("unknown-token-value-with-exactly-enough-characters-1234567890")]
    public async Task UnknownOrMalformedTokensHaveGenericRefreshAndLogoutResponses(string? token)
    {
        await using var factory = fixture.CreateLoginFactory();
        using var client = factory.CreateClient();
        using var refresh = await Refresh(client, token);
        await AssertInvalidRefresh(refresh);
        using var logout = await Logout(client, token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task SessionsOnlyExposeOwnActiveMetadataAndRevocationEnforcesOwnership()
    {
        await using var factory = fixture.CreateLoginFactory();
        await SeedAsync(factory);
        var foreignUser = await SeedAsync(factory, "foreign@identity.example");
        using var client = factory.CreateClient();
        var current = await Login(client);
        var other = await Login(client, clientId: "backend");
        var foreign = await Login(client, "foreign@identity.example");
        using var list = await Authorized(client, current, HttpMethod.Get, $"/api/v1/sessions?userId={foreignUser}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.True(list.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var items = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Single(items, item => item.GetProperty("isCurrent").GetBoolean());
        Assert.All(items, item => Assert.Equal(new[] { "absoluteExpiresAt", "clientId", "createdAt", "id", "isCurrent", "lastUsedAt", "userAgent" },
            item.EnumerateObject().Select(property => property.Name).Order().ToArray()));
        var otherId = new JsonWebToken(other.AccessToken).GetClaim("sid").Value;
        var foreignId = new JsonWebToken(foreign.AccessToken).GetClaim("sid").Value;
        using var denied = await Authorized(client, current, HttpMethod.Delete, $"/api/v1/sessions/{foreignId}");
        using var absent = await Authorized(client, current, HttpMethod.Delete, $"/api/v1/sessions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        Assert.Equal(await Code(denied), await Code(absent));
        using var revoked = await Authorized(client, current, HttpMethod.Delete, $"/api/v1/sessions/{otherId}");
        using var repeated = await Authorized(client, current, HttpMethod.Delete, $"/api/v1/sessions/{otherId}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        using var rejected = await Refresh(client, other.RefreshToken);
        await AssertInvalidRefresh(rejected);
        using var foreignRefresh = await Refresh(client, foreign.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, foreignRefresh.StatusCode);
        using var remaining = await Authorized(client, current, HttpMethod.Get);
        using var remainingJson = JsonDocument.Parse(await remaining.Content.ReadAsStringAsync());
        Assert.Single(remainingJson.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task RevokingCurrentSessionImmediatelyRejectsItsJwtOnIdentity()
    {
        await using var factory = fixture.CreateLoginFactory();
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        var id = new JsonWebToken(pair.AccessToken).GetClaim("sid").Value;
        using var revoked = await Authorized(client, pair, HttpMethod.Delete, $"/api/v1/sessions/{id}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        using var access = await Authorized(client, pair, HttpMethod.Get);
        Assert.Equal(HttpStatusCode.Unauthorized, access.StatusCode);
    }

    [Fact]
    public async Task LogoutAllRevokesEveryOwnSessionAndPreservesForeignUsers()
    {
        await using var factory = fixture.CreateLoginFactory();
        var userId = await SeedAsync(factory);
        await SeedAsync(factory, "foreign@identity.example");
        using var client = factory.CreateClient();
        var current = await Login(client);
        var other = await Login(client, clientId: "backend");
        var foreign = await Login(client, "foreign@identity.example");
        using var logout = await Authorized(client, current, HttpMethod.Post, "/api/v1/auth/logout-all");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        foreach (var pair in new[] { current, other })
        {
            using var denied = await Refresh(client, pair.RefreshToken);
            await AssertInvalidRefresh(denied);
            using var jwt = await Authorized(client, pair, HttpMethod.Get);
            Assert.Equal(HttpStatusCode.Unauthorized, jwt.StatusCode);
        }
        using var allowed = await Refresh(client, foreign.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.All(await context.Sessions.Where(session => session.UserId == userId).ToListAsync(), session => Assert.Equal(SessionRevokeReason.LogoutAll, session.RevokeReason));
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "logout_all"));
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("logout-all")]
    [InlineData("revoke")]
    public async Task ConcurrentRevocationAndRefreshNeverLeaveAnActiveReplacement(string operation)
    {
        await using var factory = fixture.CreateLoginFactory();
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        var id = new JsonWebToken(pair.AccessToken).GetClaim("sid").Value;
        var revocation = operation switch
        {
            "logout" => Logout(client, pair.RefreshToken),
            "logout-all" => Authorized(client, pair, HttpMethod.Post, "/api/v1/auth/logout-all"),
            _ => Authorized(client, pair, HttpMethod.Delete, $"/api/v1/sessions/{id}")
        };
        var responses = await Task.WhenAll(revocation, Refresh(client, pair.RefreshToken));
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, responses[0].StatusCode);
            Assert.Contains(responses[1].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        await using var context = fixture.CreateContext();
        Assert.NotNull((await context.Sessions.SingleAsync()).RevokedAt);
        Assert.All(await context.RefreshTokens.ToListAsync(), token => Assert.NotNull(token.RevokedAt));
    }

    [Fact]
    public async Task FailureBeforeCommitRollsBackRotationAndOriginalTokenRemainsUsable()
    {
        var failure = new SwitchableSaveFailure();
        await using var factory = fixture.CreateLoginFactory(configure: services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(failure)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var original = await Login(client);
        failure.Enabled = true;
        using var failed = await Refresh(client, original.RefreshToken);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await using (var context = fixture.CreateContext())
        {
            Assert.Null((await context.RefreshTokens.SingleAsync()).ConsumedAt);
            Assert.Null((await context.Sessions.SingleAsync()).RevokedAt);
            Assert.Equal(0, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "refresh_rotated"));
        }
        failure.Enabled = false;
        using var retry = await Refresh(client, original.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task LostResponseAfterCommitKeepsConsumptionAndRetryTriggersReplayRevocation()
    {
        var failure = new SwitchablePostCommitFailure();
        await using var factory = fixture.CreateLoginFactory(configure: services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(failure)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var original = await Login(client);
        failure.Enabled = true;
        using var failed = await Refresh(client, original.RefreshToken);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await using (var context = fixture.CreateContext())
        {
            Assert.Equal(2, await context.RefreshTokens.CountAsync());
            Assert.Equal(1, await context.RefreshTokens.CountAsync(token => token.ConsumedAt != null));
            Assert.Null((await context.Sessions.SingleAsync()).RevokedAt);
        }
        failure.Enabled = false;
        using var retry = await Refresh(client, original.RefreshToken);
        await AssertInvalidRefresh(retry);
        await using var after = fixture.CreateContext();
        Assert.Equal(2, await after.RefreshTokens.CountAsync());
        Assert.All(await after.RefreshTokens.ToListAsync(), token => Assert.NotNull(token.RevokedAt));
        Assert.Equal(SessionRevokeReason.RefreshReplay, (await after.Sessions.SingleAsync()).RevokeReason);
    }

    [Fact]
    public async Task FailedReplayCommitRollsBackBulkRevocationAndCanBeRetried()
    {
        var failure = new SwitchableSaveFailure();
        await using var factory = fixture.CreateLoginFactory(configure: services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(failure)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var original = await Login(client);
        using var first = await Refresh(client, original.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        failure.Enabled = true;
        using var failed = await Refresh(client, original.RefreshToken);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await using (var context = fixture.CreateContext())
        {
            Assert.Null((await context.Sessions.SingleAsync()).RevokedAt);
            Assert.All(await context.RefreshTokens.ToListAsync(), token => Assert.Null(token.RevokedAt));
            Assert.Equal(0, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "refresh_replay_detected"));
        }
        failure.Enabled = false;
        using var replay = await Refresh(client, original.RefreshToken);
        await AssertInvalidRefresh(replay);
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("logout-all")]
    [InlineData("revoke")]
    public async Task FailedRevocationDoesNotPartiallyRevokeTokens(string operation)
    {
        var failure = new SwitchableSaveFailure();
        await using var factory = fixture.CreateLoginFactory(configure: services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(failure)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        var id = new JsonWebToken(pair.AccessToken).GetClaim("sid").Value;
        failure.Enabled = true;
        using var response = await (operation switch
        {
            "logout" => Logout(client, pair.RefreshToken),
            "logout-all" => Authorized(client, pair, HttpMethod.Post, "/api/v1/auth/logout-all"),
            _ => Authorized(client, pair, HttpMethod.Delete, $"/api/v1/sessions/{id}")
        });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Null((await context.Sessions.SingleAsync()).RevokedAt);
        Assert.Null((await context.RefreshTokens.SingleAsync()).RevokedAt);
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync());
    }

    private static async Task AssertInvalidRefresh(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_refresh_token", await Code(response));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        return json.RootElement.GetProperty("code").GetString();
    }

    private sealed class TokenPair
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public string TokenType { get; set; } = "";
        public int ExpiresIn { get; set; }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class SwitchableSaveFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            Enabled ? throw new InvalidOperationException("Injected pre-commit failure.") : ValueTask.FromResult(result);
    }

    private sealed class SwitchablePostCommitFailure : DbTransactionInterceptor
    {
        public bool Enabled { get; set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
            Enabled ? throw new InvalidOperationException("Injected post-commit response failure.") : Task.CompletedTask;
    }
}
