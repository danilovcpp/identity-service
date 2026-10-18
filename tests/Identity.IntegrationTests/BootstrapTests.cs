using System.Net;
using System.Text.Json;
using Identity.Api.Controllers;
using Identity.Application.Abstractions.Cqrs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.IntegrationTests;

public sealed class BootstrapTests
{
    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task OpenApiIsOnlyPublishedInDevelopment(string environment, HttpStatusCode expectedStatus)
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(environment).ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=localhost;Database=identity_bootstrap;Username=identity"
                })));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.OK)
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/health/live", out _));
        }
    }

    [Fact]
    public async Task ProductionCompositionDiscoversTheHealthController()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production").ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=localhost;Database=identity_bootstrap;Username=identity"
                })));
        var actions = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>();

        var action = Assert.Single(actions, item => item.ActionName == "Live");
        Assert.Equal(typeof(HealthController), action.ControllerTypeInfo.AsType());
        Assert.Equal("health/live", action.AttributeRouteInfo?.Template);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Host=localhost;Database=identity;Username=identity;Include Error Detail=true")]
    public async Task InvalidDatabaseConfigurationPreventsStartup(string connection)
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production").ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = connection })));
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.DoesNotContain(connection.Length == 0 ? "Password=" : connection, error.Message);
    }

    [Fact]
    public async Task DispatchersAreScopedAndCannotBeResolvedFromRoot()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=localhost;Database=identity_bootstrap;Username=identity"
                })));
        Assert.Throws<InvalidOperationException>(() => factory.Services.GetRequiredService<IQueryDispatcher>());
        Assert.Throws<InvalidOperationException>(() => factory.Services.GetRequiredService<ICommandDispatcher>());
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        Assert.Same(first.ServiceProvider.GetRequiredService<IQueryDispatcher>(), first.ServiceProvider.GetRequiredService<IQueryDispatcher>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IQueryDispatcher>(), second.ServiceProvider.GetRequiredService<IQueryDispatcher>());
    }
}
