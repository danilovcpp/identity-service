using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Identity.Application.Abstractions.Security;
using Identity.Infrastructure.Email;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Identity.IntegrationTests;

public sealed class RegistrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("identity_registration_tests").WithUsername("identity")
        .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))).Build();
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1")
        .WithEnvironment("MP_SMTP_DISABLE_RDNS", "true")
        .WithPortBinding(1025, true).WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8025).ForPath("/"))).Build();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "identity-registration-tests-" + Guid.NewGuid().ToString("N"));
    private string KeyPath => Path.Combine(_directory, "outbox-key");
    public string JwtKeyPath => Path.Combine(_directory, "jwt-key.pem");
    public string OldPublicKeyPath => Path.Combine(_directory, "jwt-public.pem");
    public string NextJwtKeyPath => Path.Combine(_directory, "jwt-next.pem");
    public HttpClient Mailpit { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(KeyPath, RandomNumberGenerator.GetBytes(32));
        using (var rsa = RSA.Create(3072))
        {
            await File.WriteAllTextAsync(JwtKeyPath, rsa.ExportPkcs8PrivateKeyPem());
            await File.WriteAllTextAsync(OldPublicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());
        }
        using (var rsa = RSA.Create(3072)) { await File.WriteAllTextAsync(NextJwtKeyPath, rsa.ExportPkcs8PrivateKeyPem()); }
        await Task.WhenAll(_postgres.StartAsync(), _mailpit.StartAsync());
        Mailpit = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}") };
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Mailpit?.Dispose();
        await _mailpit.DisposeAsync();
        await _postgres.DisposeAsync();
        File.Delete(KeyPath);
        File.Delete(JwtKeyPath);
        File.Delete(OldPublicKeyPath);
        File.Delete(NextJwtKeyPath);
        Directory.Delete(_directory);
    }

    public IdentityDbContext CreateContext() => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    public WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?>? overrides = null, Action<IServiceCollection>? configure = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = _postgres.GetConnectionString(),
            ["Registration:Enabled"] = "true",
            ["Outbox:Enabled"] = "false",
            ["SessionCleanup:Enabled"] = "false",
            ["Outbox:PollSeconds"] = "0.05",
            ["Outbox:BaseRetrySeconds"] = "1",
            ["Outbox:MaxAttempts"] = "2",
            ["OutboxEncryption:ActiveKeyId"] = "test-ephemeral-key",
            ["OutboxEncryption:Keys:test-ephemeral-key"] = KeyPath,
            ["Email:FromAddress"] = "no-reply@identity.example",
            ["Email:VerificationUrlTemplate"] = "http://localhost:3000/verify#token={token}",
            ["Email:Smtp:Host"] = _mailpit.Hostname == "localhost" ? "127.0.0.1" : _mailpit.Hostname,
            ["Email:Smtp:Port"] = _mailpit.GetMappedPublicPort(1025).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Email:Smtp:Tls"] = "None",
            ["Email:Smtp:TimeoutSeconds"] = "10",
            ["AuthRateLimits:IpPermitLimit"] = "1000",
            ["AuthRateLimits:AccountPermitLimit"] = "1000"
        };
        if (overrides is not null) { foreach (var pair in overrides) { settings[pair.Key] = pair.Value; } }
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
            if (configure is not null) { builder.ConfigureServices(configure); }
        });
    }

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE users, credentials, sessions, refresh_tokens, one_time_tokens, outbox_messages, security_audit_events CASCADE");
        using var response = await Mailpit.DeleteAsync("/api/v1/messages");
        response.EnsureSuccessStatusCode();
    }

    public WebApplicationFactory<Program> CreateLoginFactory(Dictionary<string, string?>? overrides = null, Action<IServiceCollection>? configure = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Enabled"] = "true",
            ["Jwt:Issuer"] = "https://identity.example",
            ["Jwt:ActiveKeyId"] = "test-jwt",
            ["Jwt:PrivateKeyPath"] = JwtKeyPath,
            ["Identity:Clients:0:ClientId"] = "native",
            ["Identity:Clients:0:Audience"] = "identity-native",
            ["Identity:Clients:0:Enabled"] = "true",
            ["Identity:Clients:1:ClientId"] = "backend",
            ["Identity:Clients:1:Audience"] = "identity-backend",
            ["Identity:Clients:1:Enabled"] = "true",
            ["Identity:Clients:2:ClientId"] = "disabled",
            ["Identity:Clients:2:Audience"] = "identity-disabled",
            ["Identity:Clients:2:Enabled"] = "false"
        };
        if (overrides is not null) { foreach (var pair in overrides) { settings[pair.Key] = pair.Value; } }
        return CreateFactory(settings, services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ProtectedProbeController).Assembly);
            configure?.Invoke(services);
        });
    }

    public async Task<bool> ProcessAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().ProcessNextAsync(CancellationToken.None);
    }

    public async Task<string[]> TokensFromOutboxAsync(WebApplicationFactory<Program> factory)
    {
        await using var context = CreateContext();
        var messages = await context.OutboxMessages.AsNoTracking().Where(message => message.Payload != null).ToListAsync();
        var protector = factory.Services.GetRequiredService<IOutboxPayloadProtector>();
        return messages.Select(message =>
        {
            using var json = JsonDocument.Parse(protector.Decrypt(message.Payload!));
            return ExtractToken(json.RootElement.GetProperty("TextBody").GetString()!);
        }).ToArray();
    }

    public async Task<string> DeliveredTokenAsync()
    {
        using var messages = JsonDocument.Parse(await Mailpit.GetStringAsync("/api/v1/messages"));
        var id = messages.RootElement.GetProperty("messages")[0].GetProperty("ID").GetString();
        using var email = JsonDocument.Parse(await Mailpit.GetStringAsync($"/api/v1/message/{id}"));
        return ExtractToken(email.RootElement.GetProperty("Text").GetString()!);
    }

    public async Task<int> DeliveredCountAsync()
    {
        using var messages = JsonDocument.Parse(await Mailpit.GetStringAsync("/api/v1/messages"));
        return messages.RootElement.GetProperty("total").GetInt32();
    }

    private static string ExtractToken(string text)
    {
        var match = Regex.Match(text, "#token=([A-Za-z0-9_-]{43})", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!match.Success) { throw new InvalidOperationException("The fixture verification link was not found."); }
        return match.Groups[1].Value;
    }
}
