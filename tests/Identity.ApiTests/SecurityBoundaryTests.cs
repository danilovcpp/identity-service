using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Identity.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.ApiTests;

public sealed class SecurityBoundaryTests
{
    private const string Secret = "private-input-must-not-escape";
    private static WebApplicationFactory<Program> Factory(bool production = false, Dictionary<string, string?>? settings = null, TestLogs? logs = null)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = "Host=localhost;Database=security_boundary;Username=identity",
            ["AllowedHosts"] = "localhost;identity.example"
        };
        if (settings is not null) { foreach (var entry in settings) { configuration[entry.Key] = entry.Value; } }
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(production ? "Production" : "Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
            builder.ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(SecurityBoundaryProbeController).Assembly);
                services.AddAuthorization(options => options.AddPolicy("AlwaysDenied", policy => policy.RequireAssertion(_ => false)));
                services.AddTransient<IStartupFilter, RemoteAddressFilter>();
                if (logs is not null) { services.AddLogging(logging => logging.AddProvider(logs)); }
            });
        });
    }

    [Theory]
    [InlineData("http://localhost", "/api/v1/users/me", 400)]
    [InlineData("http://localhost", "/health/live", 200)]
    [InlineData("https://identity.example", "/api/v1/users/me", 401)]
    public async Task ProductionRequiresHttpsWithInternalHealthException(string origin, string path, int status)
    {
        await using var factory = Factory(production: true);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri(origin) });
        using var response = await client.GetAsync(path);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        if (origin == "https://identity.example") { Assert.Contains("max-age=31536000", response.Headers.GetValues("Strict-Transport-Security").Single()); }
        else { Assert.False(response.Headers.Contains("Strict-Transport-Security")); }
        if (status == 400) { await AssertProblem(response, "https_required"); Assert.Null(response.Headers.Location); }
    }

    [Theory]
    [InlineData(false, "203.0.113.10", "http", "203.0.113.10")]
    [InlineData(true, "203.0.113.10", "https", "198.51.100.50")]
    [InlineData(true, "203.0.113.11", "http", "203.0.113.10")]
    public async Task ForwardedSchemeAndIpRequireExactTrustedProxy(bool enabled, string trusted, string scheme, string ip)
    {
        await using var factory = Factory(settings: new()
        {
            ["TransportSecurity:ForwardedHeadersEnabled"] = enabled.ToString(),
            ["TransportSecurity:KnownProxies:0"] = trusted
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/security-boundary-probe/network");
        request.Headers.Add("X-Forwarded-For", "198.51.100.50");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "attacker.example");
        using var response = await client.SendAsync(request);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(scheme, json.RootElement.GetProperty("scheme").GetString());
        Assert.Equal(ip, json.RootElement.GetProperty("ip").GetString());
        Assert.Equal("localhost", json.RootElement.GetProperty("host").GetString());
    }

    [Theory]
    [InlineData("TransportSecurity:ForwardedHeadersEnabled", "true")]
    [InlineData("TransportSecurity:ForwardLimit", "0")]
    [InlineData("TransportSecurity:KnownProxies:0", "0.0.0.0")]
    [InlineData("TransportSecurity:KnownProxies:0", "::")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("FORWARDEDHEADERS_ENABLED", "true")]
    [InlineData("SecurityMaintenance:AuditRetentionDays", "0")]
    [InlineData("SecurityMaintenance:BatchSize", "1001")]
    public async Task UnsafeProductionConfigurationFailsStartup(string key, string value)
    {
        await using var factory = Factory(production: true, settings: new() { [key] = value });
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xml")]
    [InlineData("application/json")]
    public async Task ErrorContractIgnoresUnsupportedAcceptAndNeverReflectsJsonInput(string accept)
    {
        var logs = new TestLogs();
        await using var factory = Factory(logs: logs);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login?token=" + Secret);
        request.Headers.Add("Accept", accept);
        request.Headers.Add("X-Correlation-ID", Secret);
        request.Content = new StringContent("{\"password\":{\"" + Secret + "\":1}}", Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblem(response, "validation_failed");
        Assert.DoesNotContain(Secret, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(logs.Lines, value => value.Contains(Secret, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("forbidden", 403, "forbidden")]
    [InlineData("crash", 500, "request_failed")]
    [InlineData("missing", 404, "not_found")]
    public async Task FailuresHaveStableCodesAndCorrelationWithoutExceptionDetails(string action, int status, string code)
    {
        var logs = new TestLogs();
        await using var factory = Factory(logs: logs);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/security-boundary-probe/" + action + "?secret=" + Secret);
        Assert.Equal(status, (int)response.StatusCode);
        await AssertProblem(response, code);
        Assert.DoesNotContain(Secret, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(logs.Lines, value => value.Contains(Secret, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/api/v1/auth/password/reset")]
    public async Task ChunkedOversizedBodyIsRejectedWithoutReadingItAll(string path)
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new UnknownLengthContent() };
        request.Headers.TransferEncodingChunked = true;
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await AssertProblem(response, "request_too_large");
    }

    [Fact]
    public async Task CorsIsDeniedAndForgedForwardedIpDoesNotEvadeRateLimit()
    {
        await using var factory = Factory(settings: new() { ["AuthRateLimits:IpPermitLimit"] = "1" });
        using var client = factory.CreateClient();
        foreach (var index in new[] { 1, 2 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/password/forgot");
            request.Headers.Add("X-Forwarded-For", "198.51.100." + index);
            request.Headers.Add("Origin", "https://attacker.example");
            request.Content = JsonContent.Create(new { email = "absent@identity.example" });
            using var response = await client.SendAsync(request);
            Assert.Equal(index == 1 ? 202 : 429, (int)response.StatusCode);
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
            if (index == 2) { await AssertProblem(response, "rate_limited"); Assert.NotNull(response.Headers.RetryAfter); }
        }
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/users/me");
        preflight.Headers.Add("Origin", "https://attacker.example");
        preflight.Headers.Add("Access-Control-Request-Method", "PATCH");
        using var denied = await client.SendAsync(preflight);
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task AssertProblem(HttpResponseMessage response, string code)
    {
        Assert.True(response.Content.Headers.ContentType?.MediaType == "application/problem+json",
            response.Headers + " " + await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), json.RootElement.GetProperty("traceId").GetString());
        Assert.False(json.RootElement.TryGetProperty("exception", out _));
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(new byte[17000]).AsTask();
    }
    private sealed class RemoteAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("test", "true")], "test"));
                return continuation(context);
            });
            next(app);
        };
    }
    internal sealed class TestLogs : ILoggerProvider
    {
        public ConcurrentBag<string> Lines { get; } = [];
        public ILogger CreateLogger(string categoryName) => new Capture(Lines);
        public void Dispose() { }
        private sealed class Capture(ConcurrentBag<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => lines.Add(formatter(state, exception));
        }
    }
}

[Route("api/v1/security-boundary-probe")]
public sealed class SecurityBoundaryProbeController : ControllerBase
{
    [HttpGet("network"), AllowAnonymous]
    public IActionResult Network() => Ok(new { scheme = Request.Scheme, ip = HttpContext.Connection.RemoteIpAddress?.ToString(), host = Request.Host.Host });
    [HttpGet("forbidden"), Authorize(Policy = "AlwaysDenied")]
    public IActionResult Forbidden() => Ok();
    [HttpGet("crash"), AllowAnonymous]
    public IActionResult Crash() => throw new InvalidOperationException("private-input-must-not-escape");
}
