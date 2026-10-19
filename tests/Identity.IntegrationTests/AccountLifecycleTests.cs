using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Email;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.IntegrationTests;

public sealed class AccountLifecycleTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    private const string Email = "lifecycle@identity.example";
    private const string Password = "Walking over the frozen lake 42!";
    private const string NewPassword = "Sailing toward the midnight sun 73!";
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> Factory(Dictionary<string, string?>? overrides = null, Action<IServiceCollection>? configure = null)
    {
        var settings = new Dictionary<string, string?> { ["PasswordReset:Enabled"] = "true", ["Email:ResetPasswordUrlTemplate"] = "http://localhost:3000/reset#token={token}" };
        if (overrides is not null) { foreach (var pair in overrides) { settings[pair.Key] = pair.Value; } }
        return fixture.CreateLoginFactory(settings, configure);
    }

    private async Task<Guid> Seed(WebApplicationFactory<Program> factory, string email = Email, UserStatus status = UserStatus.Active)
    {
        using var scope = factory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().AddMinutes(-1);
        var user = User.Create(email, "Original", now);
        if (status != UserStatus.PendingVerification) { user.VerifyEmail(now); }
        if (status == UserStatus.Disabled) { user.Disable(now); }
        if (status == UserStatus.Deactivated) { user.Deactivate(now); }
        await using var context = fixture.CreateContext();
        context.AddRange(user, Credential.Create(user.Id, scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(Password), now));
        await context.SaveChangesAsync();
        return user.Id;
    }

    private static Task<HttpResponseMessage> LoginResponse(HttpClient client, string password = Password, string email = Email, string clientId = "native") =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password, clientId });

    private static async Task<TokenPair> Login(HttpClient client, string password = Password, string email = Email, string clientId = "native")
    {
        using var response = await LoginResponse(client, password, email, clientId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TokenPair>())!;
    }

    private static Task<HttpResponseMessage> Forgot(HttpClient client, string email = Email) => client.PostAsJsonAsync("/api/v1/auth/password/forgot", new { email });
    private static Task<HttpResponseMessage> Reset(HttpClient client, string token, string password = NewPassword) => client.PostAsJsonAsync("/api/v1/auth/password/reset", new { token, newPassword = password });
    private static Task<HttpResponseMessage> Refresh(HttpClient client, TokenPair pair) => client.PostAsJsonAsync("/api/v1/auth/refresh", new { pair.RefreshToken });

    private static async Task<HttpResponseMessage> Authorized(HttpClient client, TokenPair pair, HttpMethod method, string path = "/api/v1/users/me", object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        if (body is not null) { request.Content = JsonContent.Create(body); }
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> Change(HttpClient client, TokenPair pair, string current = Password, string next = NewPassword) =>
        Authorized(client, pair, HttpMethod.Post, "/api/v1/auth/password/change", new { currentPassword = current, newPassword = next });

    [Fact]
    public async Task ProfileOnlyExposesOwnSafeFieldsAndOnlyDisplayNameCanChange()
    {
        await using var factory = Factory();
        var owner = await Seed(factory);
        var foreign = await Seed(factory, "foreign@identity.example");
        using var client = factory.CreateClient();
        var pair = await Login(client);
        using var get = await Authorized(client, pair, HttpMethod.Get, $"/api/v1/users/me?userId={foreign}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.True(get.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "createdAt", "displayName", "email", "emailVerified", "id", "status" }, json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal(owner, json.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("active", json.RootElement.GetProperty("status").GetString());
        Assert.True(json.RootElement.GetProperty("emailVerified").GetBoolean());
        using var patch = await Authorized(client, pair, HttpMethod.Patch, body: new { displayName = "  Updated Name  " });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        using var updated = JsonDocument.Parse(await patch.Content.ReadAsStringAsync());
        Assert.Equal("Updated Name", updated.RootElement.GetProperty("displayName").GetString());
        Assert.Equal(Email, updated.RootElement.GetProperty("email").GetString());
        using var clear = await Authorized(client, pair, HttpMethod.Patch, body: new { displayName = (string?)null });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        using var rejected = await Authorized(client, pair, HttpMethod.Patch, body: new { displayName = "Hacked", email = "other@identity.example", userId = foreign });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var missing = await Authorized(client, pair, HttpMethod.Patch, body: new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var foreignRoute = await Authorized(client, pair, HttpMethod.Get, $"/api/v1/users/{foreign}");
        Assert.Equal(HttpStatusCode.NotFound, foreignRoute.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Null((await context.Users.SingleAsync(user => user.Id == owner)).DisplayName);
        Assert.Equal("Original", (await context.Users.SingleAsync(user => user.Id == foreign)).DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid\nname")]
    public async Task InvalidDisplayNamesDoNotChangeProfile(string value)
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        using var response = await Authorized(client, pair, HttpMethod.Patch, body: new { displayName = value });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await Code(response));
        await using var context = fixture.CreateContext();
        Assert.Equal("Original", (await context.Users.SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task ChangePasswordRevokesAllSessionsInvalidatesResetAndPreservesForeignUsers()
    {
        var logs = new SecretLogCapture();
        await using var factory = Factory(configure: services => services.AddLogging(logging => logging.AddProvider(logs)));
        var owner = await Seed(factory);
        await Seed(factory, "foreign@identity.example");
        using var client = factory.CreateClient();
        var first = await Login(client);
        var second = await Login(client, clientId: "backend");
        var foreign = await Login(client, email: "foreign@identity.example");
        using var forgot = await Forgot(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        using var change = await Change(client, first);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Empty(await change.Content.ReadAsStringAsync());
        foreach (var pair in new[] { first, second })
        {
            using var refresh = await Refresh(client, pair);
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
            using var profile = await Authorized(client, pair, HttpMethod.Get);
            Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
        }
        using var oldLogin = await LoginResponse(client);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var next = await Login(client, NewPassword);
        using var staleReset = await Reset(client, token);
        Assert.Equal(HttpStatusCode.BadRequest, staleReset.StatusCode);
        using var foreignProfile = await Authorized(client, foreign, HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, foreignProfile.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.All(await context.Sessions.Where(session => session.UserId == owner && session.RevokedAt != null).ToListAsync(), session => Assert.Equal(SessionRevokeReason.PasswordChanged, session.RevokeReason));
        Assert.Equal(2, await context.Sessions.CountAsync(session => session.UserId == owner && session.RevokedAt != null));
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "password_changed"));
        Assert.DoesNotContain(logs.Lines, line => new[] { Password, NewPassword, first.AccessToken, first.RefreshToken, second.RefreshToken, token, next.AccessToken }.Any(secret => line.Contains(secret, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("change")]
    [InlineData("deactivate")]
    public async Task WrongConfirmationPasswordNeverChangesState(string operation)
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        using var response = await (operation == "change" ? Change(client, pair, "Incorrect password") :
            Authorized(client, pair, HttpMethod.Post, "/api/v1/users/me/deactivate", new { password = "Incorrect password" }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_credentials", await Code(response));
        using var stillActive = await Authorized(client, pair, HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, stillActive.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(UserStatus.Active, (await context.Users.SingleAsync()).Status);
        Assert.Null((await context.Sessions.SingleAsync()).RevokedAt);
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task MailpitResetIsEncryptedSingleUseAtomicAndRevokesEverySession()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = factory.CreateClient();
        var first = await Login(client);
        var second = await Login(client, clientId: "backend");
        using var response = await Forgot(client, " LIFECYCLE@identity.example ");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var raw = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        await using (var context = fixture.CreateContext())
        {
            var token = await context.OneTimeTokens.SingleAsync();
            Assert.Equal(OneTimeTokenPurpose.PasswordReset, token.Purpose);
            Assert.Equal(TimeSpan.FromMinutes(30), token.ExpiresAt - token.CreatedAt);
            Assert.Equal(factory.Services.GetRequiredService<ITokenGenerator>().Hash(raw), token.TokenHash);
            var message = await context.OutboxMessages.SingleAsync();
            Assert.Equal("password_reset", message.Type);
            var payload = JsonSerializer.Serialize(message.Payload);
            Assert.DoesNotContain(raw, payload);
            Assert.DoesNotContain(Email, payload);
            using var envelope = JsonDocument.Parse(factory.Services.GetRequiredService<IOutboxPayloadProtector>().Decrypt(message.Payload!));
            Assert.Equal(Email, envelope.RootElement.GetProperty("Recipient").GetString());
        }
        Assert.True(await fixture.ProcessAsync(factory));
        var delivered = await fixture.DeliveredTokenAsync();
        Assert.Equal(raw, delivered);
        using var reset = await Reset(client, delivered);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var repeated = await Reset(client, delivered);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        Assert.Equal("invalid_password_reset_token", await Code(repeated));
        foreach (var pair in new[] { first, second })
        {
            using var refresh = await Refresh(client, pair);
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        using var old = await LoginResponse(client);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        await Login(client, NewPassword);
        await using var after = fixture.CreateContext();
        Assert.NotNull((await after.OneTimeTokens.SingleAsync()).ConsumedAt);
        Assert.Null((await after.OutboxMessages.SingleAsync()).Payload);
        Assert.Equal(1, await after.SecurityAuditEvents.CountAsync(audit => audit.EventType == "password_reset"));
        Assert.Equal(2, await after.Sessions.CountAsync(session => session.RevokeReason == SessionRevokeReason.PasswordReset));
        var credential = await after.Credentials.SingleAsync();
        Assert.True(credential.PasswordChangedAt > credential.CreatedAt);
    }

    [Theory]
    [InlineData(UserStatus.PendingVerification)]
    [InlineData(UserStatus.Disabled)]
    [InlineData(UserStatus.Deactivated)]
    public async Task ForgotIsGenericForUnknownAndIneligibleAccounts(UserStatus status)
    {
        await using var factory = Factory();
        await Seed(factory, status: status);
        using var client = factory.CreateClient();
        using var known = await Forgot(client);
        using var unknown = await Forgot(client, "absent@identity.example");
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.OneTimeTokens.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task NewRequestInvalidatesThePreviousLinkAndResetRecoversTemporaryLockout()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = factory.CreateClient();
        using var first = await Forgot(client);
        var previous = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        using var second = await Forgot(client);
        var current = Assert.Single((await fixture.TokensFromOutboxAsync(factory)).Except([previous]));
        using var old = await Reset(client, previous);
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        await using (var context = fixture.CreateContext())
        {
            (await context.Users.SingleAsync()).RecordFailedLogin(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15));
            await context.SaveChangesAsync();
        }
        using var reset = await Reset(client, current);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        await using var after = fixture.CreateContext();
        var user = await after.Users.SingleAsync();
        Assert.Null(user.LockedUntil);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("unknown")]
    [InlineData("malformed")]
    [InlineData("wrong-purpose")]
    [InlineData("disabled")]
    [InlineData("deactivated")]
    public async Task InvalidResetTokensNeverChangeCredentials(string scenario)
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var factory = Factory(configure: services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        var owner = await Seed(factory);
        using var client = factory.CreateClient();
        using var request = await Forgot(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        if (scenario == "expired") { clock.Advance(TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1))); }
        if (scenario == "unknown") { token = new string('x', 43); }
        if (scenario == "malformed") { token = "invalid"; }
        await using (var context = fixture.CreateContext())
        {
            if (scenario == "wrong-purpose")
            {
                token = new string('v', 43);
                context.Add(OneTimeToken.Create(owner, OneTimeTokenPurpose.EmailVerification, factory.Services.GetRequiredService<ITokenGenerator>().Hash(token), clock.GetUtcNow(), clock.GetUtcNow().AddHours(24)));
            }
            var user = await context.Users.SingleAsync();
            if (scenario == "disabled") { user.Disable(clock.GetUtcNow()); }
            if (scenario == "deactivated") { user.Deactivate(clock.GetUtcNow()); }
            await context.SaveChangesAsync();
        }
        using var response = await Reset(client, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_password_reset_token", await Code(response));
        await using var after = fixture.CreateContext();
        var credential = await after.Credentials.SingleAsync();
        Assert.Equal(credential.CreatedAt, credential.PasswordChangedAt);
        Assert.Equal(0, await after.SecurityAuditEvents.CountAsync(audit => audit.EventType == "password_reset"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentResetsForSameOrDifferentTokensOnlyChangePasswordOnce(bool differentTokens)
    {
        await using var factory = Factory();
        var owner = await Seed(factory);
        using var client = factory.CreateClient();
        using var request = await Forgot(client);
        var first = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        var second = first;
        if (differentTokens)
        {
            second = new string('z', 43);
            var now = DateTimeOffset.UtcNow;
            await using var context = fixture.CreateContext();
            context.Add(OneTimeToken.Create(owner, OneTimeTokenPurpose.PasswordReset, factory.Services.GetRequiredService<ITokenGenerator>().Hash(second), now, now.AddMinutes(30)));
            await context.SaveChangesAsync();
        }
        var responses = await Task.WhenAll(Reset(client, first), Reset(client, second));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        await using var after = fixture.CreateContext();
        Assert.Equal(1, await after.SecurityAuditEvents.CountAsync(audit => audit.EventType == "password_reset"));
        Assert.All(await after.OneTimeTokens.ToListAsync(), token => Assert.NotNull(token.ConsumedAt));
    }

    [Fact]
    public async Task DeactivationRevokesEverythingAndCannotBeReversedByResetOrLogin()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = factory.CreateClient();
        var first = await Login(client);
        var second = await Login(client, clientId: "backend");
        using var forgot = await Forgot(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        using var deactivate = await Authorized(client, first, HttpMethod.Post, "/api/v1/users/me/deactivate", new { password = Password });
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        foreach (var pair in new[] { first, second })
        {
            using var access = await Authorized(client, pair, HttpMethod.Get);
            using var refresh = await Refresh(client, pair);
            Assert.Equal(HttpStatusCode.Unauthorized, access.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        using var login = await LoginResponse(client);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("invalid_credentials", await Code(login));
        using var reset = await Reset(client, token);
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        using var afterForgot = await Forgot(client);
        Assert.Equal(HttpStatusCode.Accepted, afterForgot.StatusCode);
        await using var context = fixture.CreateContext();
        var user = await context.Users.SingleAsync();
        Assert.Equal(UserStatus.Deactivated, user.Status);
        Assert.NotNull(user.DeactivatedAt);
        Assert.Equal(Email, user.Email);
        Assert.Equal(1, await context.Credentials.CountAsync());
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
        Assert.All(await context.Sessions.ToListAsync(), session => Assert.Equal(SessionRevokeReason.AccountDeactivated, session.RevokeReason));
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "account_deactivated"));
    }

    [Theory]
    [InlineData("change")]
    [InlineData("reset")]
    [InlineData("deactivate")]
    public async Task RefreshRacingWithPasswordLifecycleNeverLeavesAnActiveReplacement(string operation)
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        using var forgot = await Forgot(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        var mutation = operation switch
        {
            "change" => Change(client, pair),
            "reset" => Reset(client, token),
            _ => Authorized(client, pair, HttpMethod.Post, "/api/v1/users/me/deactivate", new { password = Password })
        };
        var responses = await Task.WhenAll(mutation, Refresh(client, pair));
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, responses[0].StatusCode);
            Assert.Contains(responses[1].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        await using var context = fixture.CreateContext();
        Assert.All(await context.Sessions.ToListAsync(), session => Assert.NotNull(session.RevokedAt));
        Assert.All(await context.RefreshTokens.ToListAsync(), tokenRow => Assert.NotNull(tokenRow.RevokedAt));
    }

    [Theory]
    [InlineData("forgot")]
    [InlineData("reset")]
    [InlineData("change")]
    [InlineData("deactivate")]
    [InlineData("profile")]
    public async Task PreCommitFailureRollsBackAllLifecycleState(string operation)
    {
        var failure = new SaveFailure();
        await using var factory = Factory(configure: services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(failure)));
        await Seed(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        using var forgot = await Forgot(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        failure.Enabled = true;
        using var response = await (operation switch
        {
            "forgot" => Forgot(client),
            "reset" => Reset(client, token),
            "change" => Change(client, pair),
            "deactivate" => Authorized(client, pair, HttpMethod.Post, "/api/v1/users/me/deactivate", new { password = Password }),
            _ => Authorized(client, pair, HttpMethod.Patch, body: new { displayName = "Changed" })
        });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using (var context = fixture.CreateContext())
        {
            var user = await context.Users.SingleAsync();
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.Equal("Original", user.DisplayName);
            Assert.Null((await context.Sessions.SingleAsync()).RevokedAt);
            Assert.Null((await context.RefreshTokens.SingleAsync()).RevokedAt);
            Assert.Null((await context.OneTimeTokens.SingleAsync()).ConsumedAt);
            Assert.Equal(1, await context.OutboxMessages.CountAsync());
            Assert.Equal(1, await context.SecurityAuditEvents.CountAsync());
            var credential = await context.Credentials.SingleAsync();
            Assert.Equal(credential.CreatedAt, credential.PasswordChangedAt);
        }
        failure.Enabled = false;
        using var retry = await Reset(client, token);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [Fact]
    public async Task PostCommitResponseFailureDoesNotMakeResetTokenReusable()
    {
        var failure = new CommitFailure();
        await using var factory = Factory(configure: services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(failure)));
        await Seed(factory);
        using var client = factory.CreateClient();
        await Login(client);
        using var request = await Forgot(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        failure.Enabled = true;
        using var failed = await Reset(client, token);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        failure.Enabled = false;
        using var replay = await Reset(client, token);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        await Login(client, NewPassword);
        await using var context = fixture.CreateContext();
        Assert.NotNull((await context.OneTimeTokens.SingleAsync()).ConsumedAt);
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "password_reset"));
    }

    [Fact]
    public async Task ResetStateAndEncryptedDeliverySurviveHostRestart()
    {
        TokenPair pair;
        await using (var initial = Factory())
        {
            await Seed(initial);
            using var client = initial.CreateClient();
            pair = await Login(client);
            using var request = await Forgot(client);
            Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        }
        await using var restarted = Factory();
        Assert.True(await fixture.ProcessAsync(restarted));
        var token = await fixture.DeliveredTokenAsync();
        using var nextClient = restarted.CreateClient();
        using var reset = await Reset(nextClient, token);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var old = await Refresh(nextClient, pair);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        await Login(nextClient, NewPassword);
    }

    [Theory]
    [InlineData("forgot")]
    [InlineData("confirmation")]
    public async Task RecoveryAndPasswordConfirmationAreAccountRateLimited(string partition)
    {
        await using var factory = Factory(new() { ["AuthRateLimits:AccountPermitLimit"] = "1" });
        if (partition == "forgot")
        {
            using var client = factory.CreateClient();
            using var first = await Forgot(client);
            using var second = await Forgot(client, " LIFECYCLE@identity.example ");
            Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
            Assert.NotNull(second.Headers.RetryAfter);
        }
        else
        {
            await Seed(factory);
            using var client = factory.CreateClient();
            var pair = await Login(client);
            using var first = await Change(client, pair, "Wrong password");
            using var second = await Authorized(client, pair, HttpMethod.Post, "/api/v1/users/me/deactivate", new { password = "Wrong password" });
            Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        }
    }

    [Fact]
    public async Task DisabledRecoveryIsGenericAndInvalidConfigurationFailsStartup()
    {
        await using (var factory = Factory(new() { ["PasswordReset:Enabled"] = "false", ["Email:ResetPasswordUrlTemplate"] = "" }))
        {
            await Seed(factory);
            using var client = factory.CreateClient();
            using var request = await Forgot(client);
            Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
            using var reset = await Reset(client, new string('x', 43));
            Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        }
        await using var invalid = Factory(new() { ["Email:ResetPasswordUrlTemplate"] = "https://identity.example/reset?invalid={redirect}" });
        Assert.Throws<OptionsValidationException>(() => invalid.CreateClient());
    }

    [Fact]
    public async Task InvalidNewPasswordDoesNotConsumeResetOrRevokeSession()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        using var request = await Forgot(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        using var reset = await Reset(client, token, "weak");
        using var change = await Change(client, pair, next: "weak");
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        Assert.Equal("validation_failed", await Code(reset));
        await using (var context = fixture.CreateContext())
        {
            Assert.Null((await context.OneTimeTokens.SingleAsync()).ConsumedAt);
            Assert.Null((await context.Sessions.SingleAsync()).RevokedAt);
        }
        using var retry = await Reset(client, token);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [Fact]
    public async Task ResetOwnerComesFromTokenAndCannotBeOverriddenByAnotherBearerOrJson()
    {
        await using var factory = Factory();
        var owner = await Seed(factory);
        var foreign = await Seed(factory, "foreign@identity.example");
        using var client = factory.CreateClient();
        var foreignPair = await Login(client, email: "foreign@identity.example");
        using var request = await Forgot(client);
        var token = Assert.Single(await fixture.TokensFromOutboxAsync(factory));
        using var reset = await Authorized(client, foreignPair, HttpMethod.Post, "/api/v1/auth/password/reset", new { token, newPassword = NewPassword, userId = foreign });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var foreignProfile = await Authorized(client, foreignPair, HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, foreignProfile.StatusCode);
        await Login(client, NewPassword);
        await using var context = fixture.CreateContext();
        var changed = await context.Credentials.SingleAsync(credential => credential.UserId == owner);
        var unchanged = await context.Credentials.SingleAsync(credential => credential.UserId == foreign);
        Assert.True(changed.PasswordChangedAt > changed.CreatedAt);
        Assert.Equal(unchanged.CreatedAt, unchanged.PasswordChangedAt);
    }

    [Fact]
    public async Task ConcurrentPasswordChangesCannotOverwriteTheWinnerWithAnOldSession()
    {
        await using var factory = Factory();
        await Seed(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        var responses = await Task.WhenAll(Change(client, pair), Change(client, pair, next: "Another distinct personal passphrase 91!"));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "password_changed"));
    }

    [Fact]
    public async Task ProductionResetRequiresWorkerTlsTemplateAndNonTestKeys()
    {
        await using var initial = Factory(new() { ["Registration:Enabled"] = "false", ["Jwt:Enabled"] = "false", ["Outbox:Enabled"] = "false" });
        await using var production = initial.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var error = Assert.Throws<AggregateException>(() => production.CreateClient());
        Assert.All(error.Flatten().InnerExceptions, exception => Assert.IsType<OptionsValidationException>(exception));
    }

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        return json.RootElement.GetProperty("code").GetString();
    }

    [Fact]
    public async Task PasswordChangeInvalidatesOutstandingResetIssuedByAnAheadClock()
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var factory = Factory(configure: services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        var userId = await Seed(factory);
        using var client = factory.CreateClient();
        var pair = await Login(client);
        var raw = new string('s', 43);
        await using (var context = fixture.CreateContext())
        {
            var future = clock.GetUtcNow().AddMinutes(5);
            context.Add(OneTimeToken.Create(userId, OneTimeTokenPurpose.PasswordReset,
                factory.Services.GetRequiredService<ITokenGenerator>().Hash(raw), future, future.AddMinutes(30)));
            await context.SaveChangesAsync();
        }
        using var change = await Change(client, pair);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        clock.Advance(TimeSpan.FromMinutes(6));
        using var stale = await Reset(client, raw);
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        await using var after = fixture.CreateContext();
        Assert.NotNull((await after.OneTimeTokens.SingleAsync()).ConsumedAt);
    }

    private sealed class TokenPair
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
    }
    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            Enabled ? throw new InvalidOperationException("Injected pre-commit failure.") : ValueTask.FromResult(result);
    }
    private sealed class CommitFailure : DbTransactionInterceptor
    {
        public bool Enabled { get; set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
            Enabled ? throw new InvalidOperationException("Injected post-commit failure.") : Task.CompletedTask;
    }
}
