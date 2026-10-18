using Identity.Application;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Health.GetDatabaseReadiness;
using Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.UnitTests;

public sealed class ConfigurationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("not a connection string", false)]
    [InlineData("Host=localhost;Database=identity;Username=identity", true)]
    [InlineData("Host=localhost;Database=identity;Username=identity;Include Error Detail=true", false)]
    [InlineData("Host=localhost;Database=identity;Username=identity;Log Parameters=true", false)]
    public void DatabaseConfigurationRejectsMissingFieldsAndSensitiveDiagnostics(string connection, bool valid) =>
        Assert.Equal(valid, DatabaseOptions.IsValid(new DatabaseOptions { ConnectionString = connection }));

    [Fact]
    public void ApplicationExplicitlyRegistersOneScopedHandlerAndAnOverridableClock()
    {
        var services = new ServiceCollection();
        var clock = new FixedTimeProvider();
        services.AddSingleton<TimeProvider>(clock);
        services.AddApplication();
        var handler = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IQueryHandler<GetDatabaseReadinessQuery, bool>));
        Assert.Equal(ServiceLifetime.Scoped, handler.Lifetime);
        Assert.Equal(typeof(GetDatabaseReadinessHandler), handler.ImplementationType);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    private sealed class FixedTimeProvider : TimeProvider;
}
