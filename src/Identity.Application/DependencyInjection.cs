using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Application.Features.Auth.Login;
using Identity.Application.Features.Auth.Logout;
using Identity.Application.Features.Auth.LogoutAll;
using Identity.Application.Features.Auth.Refresh;
using Identity.Application.Features.Auth.Register;
using Identity.Application.Features.Auth.RequestEmailVerification;
using Identity.Application.Features.Auth.VerifyEmail;
using Identity.Application.Features.Health.GetDatabaseReadiness;
using Identity.Application.Features.Sessions.GetSessions;
using Identity.Application.Features.Sessions.RevokeSession;
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
        services.AddScoped<ICommandHandler<LoginCommand, TokenPairResult>, LoginHandler>();
        services.AddScoped<ICommandValidator<LoginCommand>, LoginValidator>();
        services.AddScoped<ICommandHandler<RefreshCommand, TokenPairResult>, RefreshHandler>();
        services.AddScoped<ICommandValidator<RefreshCommand>, RefreshValidator>();
        services.AddScoped<ICommandHandler<LogoutCommand, Unit>, LogoutHandler>();
        services.AddScoped<ICommandHandler<LogoutAllCommand, Unit>, LogoutAllHandler>();
        services.AddScoped<ICommandHandler<RevokeSessionCommand, Unit>, RevokeSessionHandler>();
        services.AddScoped<IQueryHandler<GetSessionsQuery, IReadOnlyList<SessionSummary>>, GetSessionsHandler>();
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
