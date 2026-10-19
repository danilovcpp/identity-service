using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Api.Contracts;
using Identity.Api.Controllers;
using Identity.Application.Common;
using Identity.Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.ApiTests;

public sealed class HttpSmokeTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.UseEnvironment("Production").ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Database=identity_http_smoke;Username=identity"
            })));

    public Task InitializeAsync()
    {
        _factory.ClientOptions.BaseAddress = new Uri("https://localhost");
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task LiveReturnsHealthyJson()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("healthy", health?.Status);
    }

    [Theory]
    [InlineData("GET", "/api/v1/sessions")]
    [InlineData("GET", "/api/v1/users/me")]
    [InlineData("PATCH", "/api/v1/users/me")]
    [InlineData("POST", "/api/v1/users/me/deactivate")]
    [InlineData("POST", "/api/v1/auth/password/change")]
    [InlineData("DELETE", "/api/v1/sessions/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/auth/logout-all")]
    public async Task SessionManagementRequiresBearerAuthentication(string method, string path)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("unauthorized", json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }

    [Theory]
    [InlineData("BatchSize", "0")]
    [InlineData("PollSeconds", "0")]
    [InlineData("RetentionSeconds", "0")]
    public async Task InvalidCleanupConfigurationFailsStartup(string setting, string value)
    {
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { [$"SessionCleanup:{setting}"] = value })));
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient());
    }

    [Fact]
    public async Task DisabledLoginReturns503AndPublishesNoKeys()
    {
        using var client = _factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "user@identity.example", password = "Existing password", clientId = "native" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        using var error = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.Equal("login_disabled", error.RootElement.GetProperty("code").GetString());
        using var jwks = JsonDocument.Parse(await client.GetStringAsync("/.well-known/jwks.json"));
        Assert.Empty(jwks.RootElement.GetProperty("keys").EnumerateArray());
    }

    [Theory]
    [InlineData("GET", "/api/v1/not-found", HttpStatusCode.NotFound, "not_found")]
    [InlineData("POST", "/health/live", HttpStatusCode.MethodNotAllowed, "method_not_allowed")]
    public async Task HttpErrorsReturnProblemDetails(string method, string path, HttpStatusCode status, string code)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        Assert.False(document.RootElement.TryGetProperty("exception", out _));
    }

    [Fact]
    public async Task UnhandledExceptionReturnsGenericProblemDetails()
    {
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(FailureController).Assembly)));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/failure");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(500, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("request_failed", document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("internal-test-failure", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
    }

    [Theory]
    [InlineData("validation", HttpStatusCode.BadRequest, "validation_failed")]
    [InlineData("domain", HttpStatusCode.BadRequest, "invalid_user_state")]
    [InlineData("concurrency", HttpStatusCode.Conflict, "concurrency_conflict")]
    [InlineData("too-large", HttpStatusCode.RequestEntityTooLarge, "request_too_large")]
    public async Task KnownExceptionsReturnStableErrorCodes(string path, HttpStatusCode status, string code)
    {
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(FailureController).Assembly)));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/failure/{path}");
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        if (path == "validation")
        {
            Assert.Equal("The value is required.", document.RootElement.GetProperty("errors").GetProperty("value")[0].GetString());
        }
    }

    [Fact]
    public async Task MvcModelValidationUsesTheSameProblemDetailsContract()
    {
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(FailureController).Assembly)));
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/failure/model", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.TryGetProperty("errors", out _));
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task UnavailableDatabaseReturns503WithoutConnectionDetailsAndLiveRemainsHealthy()
    {
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=127.0.0.1;Port=1;Database=unavailable_fixture;Username=identity;Timeout=1;Pooling=false"
            })));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("{\"status\":\"not_ready\"}", await response.Content.ReadAsStringAsync());
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task OversizedAuthBodyIsRejectedBeforeModelBindingWithProblemDetails()
    {
        using var client = _factory.CreateClient();
        using var body = new StringContent(new string('x', 17000), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/auth/register", body);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("request_too_large", json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }
}

// Discovered only by the test host's ApplicationPart; never shipped with the API.
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public sealed class FailureController : ApiController
{
    [HttpGet]
    public IActionResult Fail() => throw new InvalidOperationException("internal-test-failure");

    [HttpGet("validation")]
    public IActionResult Validation() => throw new RequestValidationException([new FieldValidationError("value", "required", "The value is required.")]);

    [HttpGet("domain")]
    public IActionResult Domain() => throw new DomainException("invalid_user_state", "internal-test-failure");

    [HttpGet("concurrency")]
    public IActionResult Concurrency() => throw new ConcurrencyConflictException();

    [HttpGet("too-large")]
    public IActionResult TooLarge() => throw new BadHttpRequestException("internal-test-failure", 413);

    [HttpPost("model")]
    public IActionResult Model(ValidationRequest request) => Ok();
}

public sealed class ValidationRequest
{
    [Required]
    public string? Value { get; set; }
}
