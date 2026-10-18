using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Api.Contracts;
using Identity.Api.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.ApiTests;

public sealed class HttpSmokeTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

    public Task InitializeAsync() => Task.CompletedTask;

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
}

// Discovered only by the test host's ApplicationPart; never shipped with the API.
public sealed class FailureController : ApiController
{
    [HttpGet]
    public IActionResult Fail() => throw new InvalidOperationException("internal-test-failure");
}
