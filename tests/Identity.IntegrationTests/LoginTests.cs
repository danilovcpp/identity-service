using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Identity.Api.Controllers;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit.Abstractions;

namespace Identity.IntegrationTests;

public sealed class LoginTests(RegistrationFixture fixture, ITestOutputHelper output) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    private const string Email = "login@identity.example";
    private const string Password = "Walking over the frozen lake 42!";
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static Task<HttpResponseMessage> Login(HttpClient client, string password = Password, string email = Email, string clientId = "native") =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password, clientId });

    private async Task<Guid> SeedAsync(WebApplicationFactory<Program> factory, UserStatus status = UserStatus.Active, bool oldHash = false)
    {
        using var scope = factory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().AddMinutes(-1);
        var user = User.Create(Email, "Login Test", now);
        if (status != UserStatus.PendingVerification) { user.VerifyEmail(now); }
        if (status == UserStatus.Disabled) { user.Disable(now); }
        if (status == UserStatus.Deactivated) { user.Deactivate(now); }
        var hash = oldHash ? new PasswordHasher<object>(Options.Create(new PasswordHasherOptions { IterationCount = 100_000 }))
            .HashPassword(new object(), Password) : scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(Password);
        await using var context = fixture.CreateContext();
        context.Add(user);
        context.Add(Credential.Create(user.Id, hash, now));
        await context.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task LoginCreatesAtomicSessionAndHashedRefreshAndPubliclyVerifiableAccessToken()
    {
        var logs = new CapturedLogs();
        await using var factory = fixture.CreateLoginFactory(configure: services => services.AddLogging(logging => logging.AddProvider(logs)));
        var userId = await SeedAsync(factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Identity-Tests/1.0");
        var watch = Stopwatch.StartNew();
        using var response = await Login(client, email: " LOGIN@identity.example ");
        watch.Stop();
        output.WriteLine("Login including PBKDF2, signing and PostgreSQL transaction: {0:F1} ms (single local sample, not SLA).", watch.Elapsed.TotalMilliseconds);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var pair = (await response.Content.ReadFromJsonAsync<TokenPair>())!;
        Assert.Equal("Bearer", pair.TokenType);
        Assert.Equal(900, pair.ExpiresIn);
        Assert.Equal(43, pair.RefreshToken.Length);
        Assert.All(pair.RefreshToken, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        using var jwks = JsonDocument.Parse(await client.GetStringAsync("/.well-known/jwks.json"));
        var key = Assert.Single(jwks.RootElement.GetProperty("keys").EnumerateArray());
        Assert.Equal(new[] { "alg", "e", "kid", "kty", "n", "use" }, key.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal("test-jwt", key.GetProperty("kid").GetString());
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(pair.AccessToken, ConsumerParameters(jwks.RootElement.GetRawText()));
        Assert.True(validation.IsValid);
        var token = new JsonWebToken(pair.AccessToken);
        Assert.Equal("RS256", token.Alg);
        Assert.Equal("test-jwt", token.Kid);
        Assert.Equal(userId.ToString(), token.Subject);
        Assert.Equal("access", token.GetClaim("token_use").Value);
        Assert.DoesNotContain(token.Claims, claim => claim.Type.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(900, token.ValidTo.Subtract(token.ValidFrom).TotalSeconds);
        await using var context = fixture.CreateContext();
        var session = await context.Sessions.SingleAsync();
        var refresh = await context.RefreshTokens.SingleAsync();
        Assert.Equal(userId, session.UserId);
        Assert.Equal("native", session.ClientId);
        Assert.Equal("Identity-Tests/1.0", session.UserAgent);
        Assert.Equal(TimeSpan.FromDays(30), session.AbsoluteExpiresAt - session.CreatedAt);
        Assert.Equal(session.Id, refresh.SessionId);
        Assert.Equal(TimeSpan.FromDays(7), refresh.ExpiresAt - refresh.CreatedAt);
        Assert.Equal(factory.Services.GetRequiredService<ITokenGenerator>().Hash(pair.RefreshToken), refresh.TokenHash);
        Assert.Null(refresh.ConsumedAt);
        Assert.Equal("login_success", (await context.SecurityAuditEvents.SingleAsync()).EventType);
        Assert.DoesNotContain(logs.Lines, line => line.Contains(Password, StringComparison.Ordinal) || line.Contains(pair.AccessToken, StringComparison.Ordinal)
            || line.Contains(pair.RefreshToken, StringComparison.Ordinal) || line.Contains("PRIVATE KEY", StringComparison.Ordinal) || line.Contains(Email, StringComparison.Ordinal));
        using var authorized = await Protected(client, pair.AccessToken);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("wrong-password")]
    [InlineData("pending")]
    [InlineData("disabled")]
    [InlineData("deactivated")]
    [InlineData("unknown-client")]
    [InlineData("disabled-client")]
    public async Task LoginFailuresAreGenericAndNeverCreateTokens(string scenario)
    {
        await using var factory = fixture.CreateLoginFactory();
        await SeedAsync(factory, scenario switch
        {
            "pending" => UserStatus.PendingVerification,
            "disabled" => UserStatus.Disabled,
            "deactivated" => UserStatus.Deactivated,
            _ => UserStatus.Active
        });
        using var client = factory.CreateClient();
        using var response = await Login(client, scenario == "wrong-password" ? "Incorrect password, also long!" : Password,
            scenario == "unknown" ? "absent@identity.example" : Email,
            scenario == "unknown-client" ? "absent" : scenario == "disabled-client" ? "disabled" : "native");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_credentials", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Invalid credentials", json.RootElement.GetProperty("title").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.Sessions.ToListAsync());
        Assert.Empty(await context.RefreshTokens.ToListAsync());
        Assert.Equal("login_failed", (await context.SecurityAuditEvents.SingleAsync()).EventType);
    }

    [Fact]
    public async Task ConcurrentFailuresLockExactlyAtThresholdAndExpiredLockAllowsLogin()
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var factory = fixture.CreateLoginFactory(new() { ["Login:FailureLimit"] = "3", ["Login:LockoutSeconds"] = "60" },
            services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        var failures = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Login(client, "Wrong password")));
        foreach (var failure in failures) { Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode); failure.Dispose(); }
        await using (var context = fixture.CreateContext())
        {
            var user = await context.Users.SingleAsync();
            Assert.Equal(3, user.FailedLoginCount);
            Assert.Equal(clock.GetUtcNow().AddMinutes(1).ToUnixTimeSeconds(), user.LockedUntil!.Value.ToUnixTimeSeconds());
        }
        using var locked = await Login(client);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        using var lockedFailure = await Login(client, "Wrong password");
        Assert.Equal(HttpStatusCode.Unauthorized, lockedFailure.StatusCode);
        await using (var stillLocked = fixture.CreateContext())
        {
            Assert.Equal(3, (await stillLocked.Users.SingleAsync()).FailedLoginCount);
        }
        clock.Advance(TimeSpan.FromSeconds(61));
        using var firstAfterExpiry = await Login(client, "Wrong password");
        Assert.Equal(HttpStatusCode.Unauthorized, firstAfterExpiry.StatusCode);
        await using (var resetWindow = fixture.CreateContext())
        {
            var user = await resetWindow.Users.SingleAsync();
            Assert.Equal(1, user.FailedLoginCount);
            Assert.Null(user.LockedUntil);
        }
        using var success = await Login(client);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        await using var final = fixture.CreateContext();
        var active = await final.Users.SingleAsync();
        Assert.Equal(0, active.FailedLoginCount);
        Assert.Null(active.LockedUntil);
        Assert.Equal(1, await final.Sessions.CountAsync());
    }

    [Fact]
    public async Task RehashOnLoginPreservesPasswordChangedAtAndResetsFailures()
    {
        await using var factory = fixture.CreateLoginFactory();
        await SeedAsync(factory, oldHash: true);
        await using var before = fixture.CreateContext();
        var original = await before.Credentials.AsNoTracking().SingleAsync();
        using var client = factory.CreateClient();
        using var wrong = await Login(client, "Wrong password");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using var response = await Login(client, "Ｗalking over the frozen lake 42!");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var after = fixture.CreateContext();
        var updated = await after.Credentials.SingleAsync();
        Assert.NotEqual(original.PasswordHash, updated.PasswordHash);
        Assert.Equal(original.PasswordChangedAt, updated.PasswordChangedAt);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(Identity.Application.Abstractions.Security.PasswordVerificationResult.Success,
            scope.ServiceProvider.GetRequiredService<IPasswordService>().Verify(updated.PasswordHash, Password));
        Assert.Equal(0, (await after.Users.SingleAsync()).FailedLoginCount);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("ip")]
    public async Task LoginIsRateLimitedByIpAndNormalizedAccount(string partition)
    {
        await using var factory = fixture.CreateLoginFactory(new() { [partition == "ip" ? "AuthRateLimits:IpPermitLimit" : "AuthRateLimits:AccountPermitLimit"] = "1" });
        using var client = factory.CreateClient();
        using var first = await Login(client);
        using var second = await Login(client, email: partition == "account" ? " LOGIN@identity.example " : "another@identity.example");
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.NotNull(second.Headers.RetryAfter);
        using var json = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("rate_limited", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PreCommitFailureRollsBackSessionRefreshAuditAndCounterReset()
    {
        await using var factory = fixture.CreateLoginFactory(configure: services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new FailAfterSave())));
        await SeedAsync(factory);
        await using (var initial = fixture.CreateContext())
        {
            (await initial.Users.SingleAsync()).RecordFailedLogin(DateTimeOffset.UtcNow, null);
            await initial.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        using var response = await Login(client);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.Sessions.CountAsync());
        Assert.Equal(0, await context.RefreshTokens.CountAsync());
        Assert.Equal(0, await context.SecurityAuditEvents.CountAsync());
        Assert.Equal(1, (await context.Users.SingleAsync()).FailedLoginCount);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("algorithm")]
    [InlineData("unsigned")]
    [InlineData("kid")]
    [InlineData("signature")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("token-use")]
    [InlineData("missing-session")]
    [InlineData("missing-kid")]
    [InlineData("client-audience")]
    [InlineData("user-session")]
    [InlineData("missing-iat")]
    public async Task ProtectedIdentityRejectsInvalidAccessTokens(string scenario)
    {
        var logs = new CapturedLogs();
        await using var factory = fixture.CreateLoginFactory(configure: services => services.AddLogging(logging => logging.AddProvider(logs)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        using var login = await Login(client);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var pair = (await login.Content.ReadFromJsonAsync<TokenPair>())!;
        var original = new JsonWebToken(pair.AccessToken);
        var now = DateTime.UtcNow;
        using var unrelated = RSA.Create(2048);
        var ring = factory.Services.GetRequiredService<JwtKeyRing>();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = scenario == "issuer" ? "https://wrong.example" : "https://identity.example",
            Audience = scenario == "audience" ? "wrong-audience" : scenario == "client-audience" ? "identity-backend" : "identity-native",
            IssuedAt = scenario == "missing-iat" ? null : now,
            NotBefore = scenario == "expired" ? now.AddHours(-2) : scenario == "future" ? now.AddHours(1) : now,
            Expires = scenario == "expired" ? now.AddHours(-1) : now.AddHours(2),
            SigningCredentials = scenario switch
            {
                "unsigned" => null,
                "missing-kid" => new SigningCredentials(new RsaSecurityKey(((RsaSecurityKey)ring.SigningCredentials.Key).Rsa)
                { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } }, SecurityAlgorithms.RsaSha256),
                "algorithm" => new SigningCredentials(ring.SigningCredentials.Key, SecurityAlgorithms.RsaSha512),
                "kid" => new SigningCredentials(new RsaSecurityKey(((RsaSecurityKey)ring.SigningCredentials.Key).Rsa)
                { KeyId = "absent", CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } }, SecurityAlgorithms.RsaSha256),
                "signature" => new SigningCredentials(new RsaSecurityKey(unrelated) { KeyId = "test-jwt" }, SecurityAlgorithms.RsaSha256),
                _ => ring.SigningCredentials
            },
            Claims = new Dictionary<string, object>
            {
                ["sub"] = scenario == "user-session" ? Guid.NewGuid().ToString() : original.Subject,
                ["sid"] = scenario == "missing-session" ? Guid.NewGuid().ToString() : original.GetClaim("sid").Value,
                ["client_id"] = "native",
                ["jti"] = Guid.NewGuid().ToString("N"),
                ["token_use"] = scenario == "token-use" ? "refresh" : "access"
            }
        };
        var invalid = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
        using var response = await Protected(client, invalid);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("unauthorized", json.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(invalid, json.RootElement.GetRawText());
        Assert.DoesNotContain(logs.Lines, line => line.Contains(invalid, StringComparison.Ordinal) || line.Contains(Password, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("deactivated")]
    [InlineData("revoked")]
    [InlineData("session-expired")]
    public async Task IdentityRechecksUserAndSessionEvenWithAValidSignature(string change)
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var factory = fixture.CreateLoginFactory(new() { ["Login:SessionLifetimeSeconds"] = "60" },
            services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        await SeedAsync(factory);
        using var client = factory.CreateClient();
        using var login = await Login(client);
        var pair = (await login.Content.ReadFromJsonAsync<TokenPair>())!;
        await using (var context = fixture.CreateContext())
        {
            var user = await context.Users.SingleAsync();
            var session = await context.Sessions.SingleAsync();
            if (change == "disabled") { user.Disable(clock.GetUtcNow()); }
            if (change == "deactivated") { user.Deactivate(clock.GetUtcNow()); }
            if (change == "revoked") { session.Revoke(SessionRevokeReason.UserRevoked, clock.GetUtcNow()); }
            await context.SaveChangesAsync();
        }
        if (change == "session-expired") { clock.Advance(TimeSpan.FromSeconds(61)); }
        using var response = await Protected(client, pair.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConsumerApiUsesOnlyPublicJwksAndItsOwnAudience()
    {
        await using var identity = fixture.CreateLoginFactory();
        await SeedAsync(identity);
        using var client = identity.CreateClient();
        using var native = await Login(client);
        using var backend = await Login(client, clientId: "backend");
        var nativePair = (await native.Content.ReadFromJsonAsync<TokenPair>())!;
        var backendPair = (await backend.Content.ReadFromJsonAsync<TokenPair>())!;
        var jwks = await client.GetStringAsync("/.well-known/jwks.json");
        var consumerBuilder = WebApplication.CreateBuilder();
        consumerBuilder.WebHost.UseTestServer();
        {
            var services = consumerBuilder.Services;
            services.AddRouting();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = ConsumerParameters(jwks);
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                {
                    if (context.Principal?.FindFirst("token_use")?.Value != "access") { context.Fail("Invalid access token."); }
                    return Task.CompletedTask;
                }
                };
            });
            services.AddAuthorization();
            services.AddControllers().AddApplicationPart(typeof(ExternalProbeController).Assembly);
        }
        await using var consumer = consumerBuilder.Build();
        consumer.UseRouting();
        consumer.UseAuthentication();
        consumer.UseAuthorization();
        consumer.MapControllers();
        await consumer.StartAsync();
        using var external = consumer.GetTestClient();
        using var allowed = await Protected(external, nativePair.AccessToken, "/external-probe");
        using var rejected = await Protected(external, backendPair.AccessToken, "/external-probe");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [Fact]
    public async Task RestartAndRotationPreserveIssuedTokensWithRetainedPublicKeys()
    {
        TokenPair pair;
        await using (var first = fixture.CreateLoginFactory())
        {
            await SeedAsync(first);
            using var client = first.CreateClient();
            using var login = await Login(client);
            pair = (await login.Content.ReadFromJsonAsync<TokenPair>())!;
        }
        await using (var restart = fixture.CreateLoginFactory())
        {
            using var client = restart.CreateClient();
            using var response = await Protected(client, pair.AccessToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        await using (var rotated = fixture.CreateLoginFactory(new()
        {
            ["Jwt:ActiveKeyId"] = "test-next",
            ["Jwt:PrivateKeyPath"] = fixture.NextJwtKeyPath,
            ["Jwt:PublicKeys:test-jwt"] = fixture.OldPublicKeyPath
        }))
        {
            using var client = rotated.CreateClient();
            using var response = await Protected(client, pair.AccessToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jwks = await client.GetStringAsync("/.well-known/jwks.json");
            Assert.Equal(2, new JsonWebKeySet(jwks).Keys.Count);
            Assert.True((await new JsonWebTokenHandler().ValidateTokenAsync(pair.AccessToken, ConsumerParameters(jwks))).IsValid);
            using var nextLogin = await Login(client);
            var next = (await nextLogin.Content.ReadFromJsonAsync<TokenPair>())!;
            Assert.Equal("test-next", new JsonWebToken(next.AccessToken).Kid);
        }
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.Sessions.CountAsync());
    }

    [Fact]
    public async Task MissingPrivateKeyStopsStartupWithoutLeakingPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "identity-missing-jwt-" + Guid.NewGuid().ToString("N"));
        await using var factory = fixture.CreateLoginFactory(new() { ["Jwt:PrivateKeyPath"] = path });
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Equal("JWT key configuration could not be loaded.", error.Message);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(path, error.ToString());
    }

    [Fact]
    public async Task ProductionRejectsTestJwtKeysAndProtectedControllersRejectAnonymousRequests()
    {
        await using var initial = fixture.CreateLoginFactory(new() { ["Registration:Enabled"] = "false" });
        await using var production = initial.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var error = Assert.Throws<AggregateException>(() => production.CreateClient());
        Assert.All(error.Flatten().InnerExceptions, exception => Assert.IsType<OptionsValidationException>(exception));
        using var client = initial.CreateClient();
        using var response = await client.GetAsync("/api/v1/protected-probe");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static TokenValidationParameters ConsumerParameters(string jwks) => new()
    {
        ValidIssuer = "https://identity.example",
        ValidAudience = "identity-native",
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidateLifetime = true,
        IssuerSigningKeys = new JsonWebKeySet(jwks).GetSigningKeys(),
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        TryAllIssuerSigningKeys = false,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    private static async Task<HttpResponseMessage> Protected(HttpClient client, string token, string path = "/api/v1/protected-probe")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
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

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected failure before commit.");
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public System.Collections.Concurrent.ConcurrentBag<string> Lines { get; } = [];
        public ILogger CreateLogger(string categoryName) => new Capture(Lines);
        public void Dispose() { }
        private sealed class Capture(System.Collections.Concurrent.ConcurrentBag<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => lines.Add(formatter(state, exception));
        }
    }
}

// These controllers exist only in the test assembly; production has no probe/profile/session endpoints.
[Route("api/v1/protected-probe")]
public sealed class ProtectedProbeController : ApiController
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "authorized" });
}

[ApiController]
[Route("external-probe")]
[Authorize]
public sealed class ExternalProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
