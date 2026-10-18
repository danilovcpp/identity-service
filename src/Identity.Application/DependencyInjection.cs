using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Health.GetDatabaseReadiness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IQueryHandler<GetDatabaseReadinessQuery, bool>, GetDatabaseReadinessHandler>();
        return services;
    }
}
