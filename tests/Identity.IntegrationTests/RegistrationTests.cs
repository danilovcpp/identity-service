using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Identity.IntegrationTests;

public sealed class RegistrationTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    private const string Password = "Walking over the frozen lake 42!";
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static Task<HttpResponseMessage> Register(HttpClient client, string email = "user@identity.example", string password = Password) =>
        client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Example User", clientId = "native" });

    private static Task<HttpResponseMessage> Verify(HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/v1/auth/email/verify", new { token });

    [Fact]
    public async Task RegistrationEncryptsOutboxAndMailpitVerificationActivatesTheUserOnce()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        using var registered = await Register(client);
        Assert.Equal(HttpStatusCode.Accepted, registered.StatusCode);
        await using (var context = fixture.CreateContext())
        {
            var user = await context.Users.AsNoTracking().SingleAsync();
            Assert.Equal(UserStatus.PendingVerification, user.Status);
            Assert.Null(user.EmailVerifiedAt);
            Assert.Single(await context.Credentials.AsNoTracking().ToListAsync());
            Assert.Single(await context.OneTimeTokens.AsNoTracking().ToListAsync());
            var message = await context.OutboxMessages.AsNoTracking().SingleAsync();
            var payload = JsonSerializer.Serialize(message.Payload);
            var token = (await fixture.TokensFromOutboxAsync(factory)).Single();
            Assert.False(payload.Contains(token, StringComparison.Ordinal));
            Assert.False(payload.Contains("user@identity.example", StringComparison.Ordinal));
            Assert.False(payload.Contains(Password, StringComparison.Ordinal));
            using var passwordScope = factory.Services.CreateScope();
            Assert.True(passwordScope.ServiceProvider.GetRequiredService<IPasswordService>().Verify((await context.Credentials.SingleAsync()).PasswordHash, Password)
                == PasswordVerificationResult.Success);
        }
        Assert.True(await fixture.ProcessAsync(factory));
        Assert.Equal(1, await fixture.DeliveredCountAsync());
        var delivered = await fixture.DeliveredTokenAsync();
        using var verified = await Verify(client, delivered);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var repeated = await Verify(client, delivered);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        Assert.Equal("invalid_verification_token", await ErrorCode(repeated));
        await using var verification = fixture.CreateContext();
        var active = await verification.Users.AsNoTracking().SingleAsync();
        Assert.Equal(UserStatus.Active, active.Status);
        Assert.NotNull(active.EmailVerifiedAt);
        Assert.NotNull((await verification.OneTimeTokens.AsNoTracking().SingleAsync()).ConsumedAt);
        Assert.Null((await verification.OutboxMessages.AsNoTracking().SingleAsync()).Payload);
        Assert.Equal(2, await verification.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task DuplicateAndConcurrentRegistrationsReturnTheSame202WithoutPartialRecords()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Register(client, " User@identity.example "), Register(client, "user@identity.example"));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
            Assert.Equal(await responses[0].Content.ReadAsStringAsync(), await responses[1].Content.ReadAsStringAsync());
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        using var duplicate = await Register(client);
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(1, await context.Credentials.CountAsync());
        Assert.Equal(1, await context.OneTimeTokens.CountAsync());
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task ResendIsGenericForUnknownPendingAndActiveUsers()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        using var registered = await Register(client);
        using var pending = await client.PostAsJsonAsync("/api/v1/auth/email/verification-request", new { email = "user@identity.example" });
        using var absent = await client.PostAsJsonAsync("/api/v1/auth/email/verification-request", new { email = "unknown@identity.example" });
        Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, absent.StatusCode);
        Assert.Equal(await pending.Content.ReadAsStringAsync(), await absent.Content.ReadAsStringAsync());
        var tokens = await fixture.TokensFromOutboxAsync(factory);
        Assert.Equal(2, tokens.Length);
        using var verify = await Verify(client, tokens[0]);
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        using var active = await client.PostAsJsonAsync("/api/v1/auth/email/verification-request", new { email = "user@identity.example" });
        Assert.Equal(await absent.Content.ReadAsStringAsync(), await active.Content.ReadAsStringAsync());
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.OneTimeTokens.CountAsync());
        Assert.Equal(2, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task ExpiredUnknownAndMalformedTokensShareTheSameFailureCode()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        await using var factory = fixture.CreateFactory(configure: services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        });
        using var client = factory.CreateClient();
        using var registered = await Register(client);
        var token = (await fixture.TokensFromOutboxAsync(factory)).Single();
        clock.Advance(TimeSpan.FromHours(24));
        using var expired = await Verify(client, token);
        using var unknown = await Verify(client, new string('a', 43));
        using var malformed = await Verify(client, "invalid");
        foreach (var response in new[] { expired, unknown, malformed })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_verification_token", await ErrorCode(response));
        }
        await using var context = fixture.CreateContext();
        Assert.Equal(UserStatus.PendingVerification, (await context.Users.SingleAsync()).Status);
        Assert.Null((await context.OneTimeTokens.SingleAsync()).ConsumedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentVerificationOfSameOrDifferentTokensActivatesOnlyOnce(bool differentTokens)
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        using var registered = await Register(client);
        if (differentTokens)
        {
            using var resend = await client.PostAsJsonAsync("/api/v1/auth/email/verification-request", new { email = "user@identity.example" });
        }
        var tokens = await fixture.TokensFromOutboxAsync(factory);
        var responses = await Task.WhenAll(Verify(client, tokens[0]), Verify(client, differentTokens ? tokens[1] : tokens[0]));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.OneTimeTokens.CountAsync(token => token.ConsumedAt != null));
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "verify_email"));
    }

    [Fact]
    public async Task ErrorAfterSaveBeforeCommitRollsBackUserCredentialTokenOutboxAndAudit()
    {
        await using var factory = fixture.CreateFactory(configure: services =>
            services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(new FailAfterSave())));
        using var client = factory.CreateClient();
        using var result = await Register(client);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.Users.CountAsync());
        Assert.Equal(0, await context.Credentials.CountAsync());
        Assert.Equal(0, await context.OneTimeTokens.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
        Assert.Equal(0, await context.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task VerificationFailureBeforeCommitRollsBackBothActivationAndTokenConsumption()
    {
        var failure = new FailAfterSave { Enabled = false };
        await using var factory = fixture.CreateFactory(configure: services =>
            services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(failure)));
        using var client = factory.CreateClient();
        using var registered = await Register(client);
        var token = (await fixture.TokensFromOutboxAsync(factory)).Single();
        failure.Enabled = true;
        using var failed = await Verify(client, token);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await using (var context = fixture.CreateContext())
        {
            Assert.Equal(UserStatus.PendingVerification, (await context.Users.SingleAsync()).Status);
            Assert.Null((await context.OneTimeTokens.SingleAsync()).ConsumedAt);
            Assert.Equal(0, await context.SecurityAuditEvents.CountAsync(audit => audit.EventType == "verify_email"));
        }
        failure.Enabled = false;
        using var retried = await Verify(client, token);
        Assert.Equal(HttpStatusCode.NoContent, retried.StatusCode);
    }

    [Fact]
    public async Task SmtpFailurePersistsEncryptedPayloadAndLaterRetryDeliversIt()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        var sender = new SwitchableSender();
        await using var factory = fixture.CreateFactory(configure: services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(sender);
        });
        using var client = factory.CreateClient();
        using var registered = await Register(client);
        Assert.True(await fixture.ProcessAsync(factory));
        await using (var context = fixture.CreateContext())
        {
            var failed = await context.OutboxMessages.SingleAsync();
            Assert.Equal(1, failed.AttemptCount);
            Assert.Equal("smtp_unavailable", failed.LastError);
            Assert.NotNull(failed.Payload);
            Assert.Null(failed.ProcessedAt);
        }
        Assert.False(await fixture.ProcessAsync(factory));
        sender.Fail = false;
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(await fixture.ProcessAsync(factory));
        Assert.Equal(1, sender.Deliveries);
        await using var verified = fixture.CreateContext();
        Assert.Null((await verified.OutboxMessages.SingleAsync()).Payload);
    }

    [Fact]
    public async Task RealSmtpConnectionFailureDoesNotLoseAnOutboxMessage()
    {
        await using var factory = fixture.CreateFactory(new() { ["Email:Smtp:Host"] = "127.0.0.1", ["Email:Smtp:Port"] = "1" });
        using var client = factory.CreateClient();
        using var registered = await Register(client);
        Assert.True(await fixture.ProcessAsync(factory));
        await using var context = fixture.CreateContext();
        var message = await context.OutboxMessages.SingleAsync();
        Assert.Equal("smtp_unavailable", message.LastError);
        Assert.NotNull(message.Payload);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public async Task RetryLimitStopsPoisonMessageWithoutDeletingItsEncryptedPayload()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        await using var factory = fixture.CreateFactory(configure: services =>
        {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            services.RemoveAll<IEmailSender>(); services.AddSingleton<IEmailSender>(new SwitchableSender());
        });
        using var client = factory.CreateClient();
        using var registered = await Register(client);
        Assert.True(await fixture.ProcessAsync(factory));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(await fixture.ProcessAsync(factory));
        clock.Advance(TimeSpan.FromHours(1));
        Assert.False(await fixture.ProcessAsync(factory));
        await using var context = fixture.CreateContext();
        var message = await context.OutboxMessages.SingleAsync();
        Assert.Equal(2, message.AttemptCount);
        Assert.NotNull(message.Payload);
    }

    [Fact]
    public async Task ConcurrentProcessorsUseSkipLockedAndDeliverEachMessageOnce()
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        using var first = await Register(client, "one@identity.example");
        using var second = await Register(client, "two@identity.example");
        var processed = await Task.WhenAll(fixture.ProcessAsync(factory), fixture.ProcessAsync(factory));
        Assert.All(processed, Assert.True);
        Assert.Equal(2, await fixture.DeliveredCountAsync());
        Assert.False(await fixture.ProcessAsync(factory));
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.OutboxMessages.CountAsync(message => message.ProcessedAt != null && message.Payload == null));
    }

    [Fact]
    public async Task BackgroundWorkerDeliversToMailpit()
    {
        await using var factory = fixture.CreateFactory(new() { ["Outbox:Enabled"] = "true" });
        using var client = factory.CreateClient();
        using var registered = await Register(client);
        for (var index = 0; index < 400 && await fixture.DeliveredCountAsync() == 0; index++) { await Task.Delay(50); }
        Assert.Equal(1, await fixture.DeliveredCountAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InitialIpAndAccountRateLimitsReturn429WithRetryAfter(bool byIp)
    {
        await using var factory = fixture.CreateFactory(new()
        {
            ["AuthRateLimits:IpPermitLimit"] = byIp ? "1" : "1000",
            ["AuthRateLimits:AccountPermitLimit"] = byIp ? "1000" : "1"
        });
        using var client = factory.CreateClient();
        using var first = await client.PostAsJsonAsync("/api/v1/auth/email/verification-request", new { email = "unknown@identity.example" });
        using var second = await client.PostAsJsonAsync("/api/v1/auth/email/verification-request", new { email = " Unknown@identity.example " });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("rate_limited", await ErrorCode(second));
        Assert.True(second.Headers.Contains("Retry-After"));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("passwordpassword")]
    public async Task InvalidPasswordsDoNotCreateRecords(string password)
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        using var result = await Register(client, password: password);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("validation_failed", await ErrorCode(result));
        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.Users.CountAsync());
    }

    [Fact]
    public async Task ProductionRefusesDevelopmentEmailAndKeyConfiguration()
    {
        await using var initial = fixture.CreateFactory(new() { ["Outbox:Enabled"] = "true" });
        await using var production = initial.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var failure = Assert.Throws<AggregateException>(() => production.CreateClient());
        Assert.All(failure.Flatten().InnerExceptions, error => Assert.IsType<OptionsValidationException>(error));
    }

    [Fact]
    public async Task MissingSecretFilePreventsPublicRegistrationStartupWithoutLeakingItsPath()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "identity-missing-key-" + Guid.NewGuid().ToString("N"));
        await using var factory = fixture.CreateFactory(new() { ["OutboxEncryption:Keys:test-ephemeral-key"] = missingPath });
        var failure = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.DoesNotContain(missingPath, failure.Message);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("clientId")]
    [InlineData("displayName")]
    public async Task InputValidationBlocksInvalidRegistrationFields(string field)
    {
        await using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = field == "email" ? "user@identity.example\r\n" : "user@identity.example",
            password = Password,
            clientId = field == "clientId" ? "" : "native",
            displayName = field == "displayName" ? new string('x', 121) : "Example"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.Users.CountAsync());
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString();
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; } = true;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            Enabled ? throw new InvalidOperationException("A deliberately injected pre-commit failure.") : ValueTask.FromResult(result);
    }

    private sealed class SwitchableSender : IEmailSender
    {
        public bool Fail { get; set; } = true;
        public int Deliveries { get; private set; }
        public Task SendAsync(EmailMessage message, Guid messageId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Fail) { throw new IOException("A deliberately injected delivery failure."); }
            Deliveries++;
            return Task.CompletedTask;
        }
    }
}
