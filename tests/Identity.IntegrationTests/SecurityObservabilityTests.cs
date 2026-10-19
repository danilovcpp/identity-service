using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Maintenance;
using Identity.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Identity.IntegrationTests;

[CollectionDefinition("Security telemetry", DisableParallelization = true)]
public sealed class SecurityTelemetryCollection;

[Collection("Security telemetry")]
public sealed class SecurityObservabilityTests(RegistrationFixture fixture, ITestOutputHelper output) : IClassFixture<RegistrationFixture>, IAsyncLifetime
{
    private const string Email = "observability@identity.example";
    private const string Password = "Walking over the frozen lake 42!";
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private async Task Seed(IServiceProvider provider, UserStatus status = UserStatus.Active)
    {
        using var scope = provider.CreateScope();
        var now = TimeProvider.System.GetUtcNow();
        var user = User.Create(Email, null, now);
        user.VerifyEmail(now);
        if (status == UserStatus.Disabled) { user.Disable(now); }
        if (status == UserStatus.Deactivated) { user.Deactivate(now); }
        await using var context = fixture.CreateContext();
        context.AddRange(user, Credential.Create(user.Id, scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(Password), now));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task AuthCountersOnlyDescribeCommittedEventsWithBoundedLabelsAndSafeStructuredLogs()
    {
        var logs = new SecretLogCapture();
        await using var factory = fixture.CreateLoginFactory(configure: services => services.AddLogging(logging => logging.AddProvider(logs)));
        _ = factory.Services.GetRequiredService<AuthMetrics>();
        var events = new ConcurrentBag<(string Name, long Value, string Tags)>();
        using var listener = Listen(events);
        using (var scope = factory.Services.CreateScope())
        {
            var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await using var transaction = await work.BeginTransactionAsync(CancellationToken.None);
            scope.ServiceProvider.GetRequiredService<Identity.Infrastructure.Persistence.IdentityDbContext>().Add(
                SecurityAuditEvent.Create(SecurityEventType.LoginFailed, null, null, "rolled_back", new AuditMetadata(), DateTimeOffset.UtcNow));
            await work.SaveChangesAsync(CancellationToken.None);
            Assert.Empty(events);
            await transaction.RollbackAsync(CancellationToken.None);
        }
        Assert.Empty(events);
        await Seed(factory.Services);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password, clientId = "native" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = fixture.CreateContext();
        var audit = await context.SecurityAuditEvents.SingleAsync();
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), audit.CorrelationId);
        Assert.Contains(events, entry => entry.Name == "identity.auth.events" && entry.Value == 1 && entry.Tags == "event=login_success");
        Assert.DoesNotContain(events, entry => entry.Tags.Contains(Email, StringComparison.Ordinal) || entry.Tags.Contains(audit.UserId!.Value.ToString(), StringComparison.Ordinal));
        Assert.Contains(logs.Lines, line => line.Contains("login_success", StringComparison.Ordinal) && line.Contains(audit.CorrelationId, StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Lines, line => line.Contains(Password, StringComparison.Ordinal) || line.Contains(Email, StringComparison.Ordinal));
        await using var nextScope = factory.Services.CreateAsyncScope();
        await nextScope.ServiceProvider.GetRequiredService<SecurityMaintenanceProcessor>().ProcessBatchAsync(CancellationToken.None);
        listener.RecordObservableInstruments();
        Assert.Contains(events, entry => entry.Name == "identity.auth.sessions.active" && entry.Value == 1 && entry.Tags == "");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("wrong")]
    [InlineData("disabled")]
    [InlineData("deactivated")]
    public async Task EnumerationPathsAlwaysVerifyPasswordAndUnknownClientIsNotPersistedAsAuditPii(string scenario)
    {
        var called = 0;
        await using var factory = fixture.CreateLoginFactory(configure: services =>
        {
            var original = services.Single(descriptor => descriptor.ServiceType == typeof(ILoginPasswordVerifier));
            services.Remove(original);
            services.AddSingleton<ILoginPasswordVerifier>(provider => new CountedVerifier(
                (ILoginPasswordVerifier)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!), () => Interlocked.Increment(ref called)));
        });
        await Seed(factory.Services, scenario == "disabled" ? UserStatus.Disabled : scenario == "deactivated" ? UserStatus.Deactivated : UserStatus.Active);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = scenario == "unknown" ? "missing@identity.example" : Email,
            password = scenario == "wrong" ? "Incorrect password" : Password,
            clientId = "untrusted@identity.example"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, called);
        await using var context = fixture.CreateContext();
        var audit = await context.SecurityAuditEvents.SingleAsync();
        Assert.Null(audit.ClientId);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_credentials", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvalidBodiesAndHeadersNeverEnterLogsEvenWithFrameworkInformationLevel()
    {
        var logs = new SecretLogCapture();
        await using var factory = fixture.CreateLoginFactory(new() { ["Logging:LogLevel:Microsoft.AspNetCore"] = "Information" },
            services => services.AddLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient();
        const string secret = "token-or-pii-sentinel";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login?password=" + secret);
        request.Headers.Add("Authorization", "Bearer " + secret);
        request.Headers.Add("User-Agent", secret);
        request.Content = JsonContent.Create(new { email = secret, password = new { value = secret } });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(secret, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(logs.Lines, line => line.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LocalLoginAndRefreshMeasurementsExerciseProductionCryptoAndTransactions()
    {
        await using var factory = fixture.CreateLoginFactory();
        await Seed(factory.Services);
        using var client = factory.CreateClient();
        var login = new List<double>();
        var refresh = new List<double>();
        for (var index = 0; index < 5; index++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password, clientId = "native" });
            login.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            watch.Restart();
            using var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = token.RootElement.GetProperty("refreshToken").GetString() });
            refresh.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        }
        output.WriteLine("Five sequential local samples, PBKDF2=600000, PostgreSQL; not a production SLA. Login median/max {0:F1}/{1:F1} ms; refresh median/max {2:F1}/{3:F1} ms.",
            login.Order().ElementAt(2), login.Max(), refresh.Order().ElementAt(2), refresh.Max());
    }

    [Fact]
    public async Task BrokenTelemetryListenerCannotTurnCommittedLoginIntoFailure()
    {
        await using var factory = fixture.CreateLoginFactory();
        await Seed(factory.Services);
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, subscriber) =>
        {
            if (instrument.Meter.Name is "Identity.Auth" or "Identity.Http") { subscriber.EnableMeasurementEvents(instrument); }
        }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => throw new InvalidOperationException("broken telemetry"));
        listener.Start();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password, clientId = "native" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Sessions.CountAsync());
        Assert.Equal(1, await context.SecurityAuditEvents.CountAsync());
    }

    private static MeterListener Listen(ConcurrentBag<(string Name, long Value, string Tags)> events)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, subscriber) =>
        {
            if (instrument.Meter.Name == "Identity.Auth") { subscriber.EnableMeasurementEvents(instrument); }
        }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            events.Add((instrument.Name, value, string.Join(",", tags.ToArray().Select(tag => tag.Key + "=" + tag.Value))));
        });
        listener.Start();
        return listener;
    }
    private sealed class CountedVerifier(ILoginPasswordVerifier inner, Action called) : ILoginPasswordVerifier
    {
        public PasswordVerificationResult Verify(string? hash, string password) { called(); return inner.Verify(hash, password); }
    }
}
