using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

internal static class LoginDependencyInjection
{
    public static IServiceCollection AddLogin(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration("Jwt")
            .Validate<IServiceProvider>((options, provider) => JwtOptions.IsValid(options,
                provider.GetService<IHostEnvironment>()?.IsProduction() == true), "JWT configuration is missing or insecure.").ValidateOnStart();
        services.AddOptions<IdentityClientOptions>().BindConfiguration("Identity")
            .Validate<IOptions<JwtOptions>>((options, jwt) => !jwt.Value.Enabled || IdentityClientOptions.IsValid(options),
                "Enabled JWT authentication requires valid, unique clients and audiences.").ValidateOnStart();
        services.AddOptions<LoginOptions>().BindConfiguration("Login")
            .Validate(options => options.FailureLimit is >= 2 and <= 20 && options.LockoutSeconds is >= 60 and <= 3600
                && options.RefreshLifetimeSeconds is >= 60 and <= 604800 && options.SessionLifetimeSeconds is >= 60 and <= 2592000,
                "Login and session limits are invalid.").ValidateOnStart();
        services.AddSingleton<ILoginSettings, LoginSettings>();
        services.AddSingleton<ILoginPasswordVerifier, LoginPasswordVerifier>();
        services.AddSingleton<JwtKeyRing>();
        services.AddHostedService(provider => provider.GetRequiredService<JwtKeyRing>());
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<ILoginRepository, LoginRepository>();
        return services;
    }
}
