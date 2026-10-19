using Identity.Application.Abstractions.Persistence;
using Identity.Infrastructure.Email;
using Identity.Infrastructure.Maintenance;
using Identity.Infrastructure.Observability;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Repositories;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .Validate(DatabaseOptions.IsValid, "Database configuration is required and must disable error detail and parameter logging.")
            .ValidateOnStart();
        services.AddDbContext<IdentityDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICredentialRepository, CredentialRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<AuthMetrics>();
        services.AddScoped<IDatabaseReadinessProbe, DatabaseReadinessProbe>();
        // Validate/load JWT and outbox key rings before starting the email worker.
        services.AddLogin();
        services.AddRegistrationAndEmail();
        services.AddOptions<SessionCleanupOptions>().BindConfiguration("SessionCleanup")
            .Validate(options => options.BatchSize is >= 1 and <= 100 && options.PollSeconds is >= 1 and <= 3600
                && options.RetentionSeconds is >= 3600 and <= 604800, "Session cleanup limits are invalid.").ValidateOnStart();
        services.AddScoped<SessionCleanupProcessor>();
        services.AddHostedService<SessionCleanupWorker>();
        services.AddOptions<SecurityMaintenanceOptions>().BindConfiguration("SecurityMaintenance")
            .Validate(SecurityMaintenanceOptions.IsValid, "Security maintenance limits are invalid.").ValidateOnStart();
        services.AddScoped<SecurityMaintenanceProcessor>();
        services.AddHostedService<SecurityMaintenanceWorker>();
        return services;
    }
}
