using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Application.Features.Auth.Register;
using Identity.Application.Features.Auth.RequestEmailVerification;
using Identity.Application.Features.Auth.VerifyEmail;
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
        services.AddSingleton<IPasswordPolicy, PasswordPolicy>();
        services.AddScoped<OperationContext>();
        services.AddScoped<ICommandHandler<RegisterCommand, Unit>, RegisterHandler>();
        services.AddScoped<ICommandValidator<RegisterCommand>, RegisterValidator>();
        services.AddScoped<ICommandHandler<RequestEmailVerificationCommand, Unit>, RequestEmailVerificationHandler>();
        services.AddScoped<ICommandValidator<RequestEmailVerificationCommand>, RequestEmailVerificationValidator>();
        services.AddScoped<ICommandHandler<VerifyEmailCommand, Unit>, VerifyEmailHandler>();
        services.AddScoped<ICommandValidator<VerifyEmailCommand>, VerifyEmailValidator>();
        services.AddScoped<IQueryHandler<GetDatabaseReadinessQuery, bool>, GetDatabaseReadinessHandler>();
        return services;
    }
}
