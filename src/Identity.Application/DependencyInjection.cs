using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Access.GrantAdministrator;
using Identity.Application.Features.Admin.Access.RevokeAdministrator;
using Identity.Application.Features.Admin.Auth.GetMe;
using Identity.Application.Features.Admin.Auth.Login;
using Identity.Application.Features.Admin.Auth.Logout;
using Identity.Application.Features.Admin.Auth.ValidateSession;
using Identity.Application.Features.Auth.Login;
using Identity.Application.Features.Auth.Logout;
using Identity.Application.Features.Auth.LogoutAll;
using Identity.Application.Features.Auth.Refresh;
using Identity.Application.Features.Auth.Register;
using Identity.Application.Features.Auth.RequestEmailVerification;
using Identity.Application.Features.Auth.RequestPasswordReset;
using Identity.Application.Features.Auth.ResetPassword;
using Identity.Application.Features.Auth.VerifyEmail;
using Identity.Application.Features.Health.GetDatabaseReadiness;
using Identity.Application.Features.Sessions.GetSessions;
using Identity.Application.Features.Sessions.RevokeSession;
using Identity.Application.Features.Users.ChangePassword;
using Identity.Application.Features.Users.DeactivateAccount;
using Identity.Application.Features.Users.GetCurrentUser;
using Identity.Application.Features.Users.UpdateProfile;
using Identity.Domain.Enums;
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
        services.AddScoped<SessionRevoker>();
        services.AddScoped<AdministratorGuard>();
        services.AddScoped<ICommandHandler<GrantAdministratorCommand, Unit>, GrantAdministratorHandler>();
        services.AddScoped<ICommandHandler<RevokeAdministratorCommand, Unit>, RevokeAdministratorHandler>();
        services.AddScoped<ICommandHandler<AdminLoginCommand, AdminLoginResult>, AdminLoginHandler>();
        services.AddScoped<ICommandValidator<AdminLoginCommand>, AdminLoginValidator>();
        services.AddScoped<ICommandHandler<AdminLogoutCommand, Unit>, AdminLogoutHandler>();
        services.AddScoped<ICommandHandler<ValidateAdminSessionCommand, AdministratorRole?>, ValidateAdminSessionHandler>();
        services.AddScoped<IQueryHandler<GetAdminMeQuery, AdminProfile>, GetAdminMeHandler>();
        services.AddScoped<IQueryHandler<GetCurrentUserQuery, UserProfile>, GetCurrentUserHandler>();
        services.AddScoped<ICommandHandler<UpdateProfileCommand, UserProfile>, UpdateProfileHandler>();
        services.AddScoped<ICommandValidator<UpdateProfileCommand>, UpdateProfileValidator>();
        services.AddScoped<ICommandHandler<ChangePasswordCommand, Unit>, ChangePasswordHandler>();
        services.AddScoped<ICommandValidator<ChangePasswordCommand>, ChangePasswordValidator>();
        services.AddScoped<ICommandHandler<DeactivateAccountCommand, Unit>, DeactivateAccountHandler>();
        services.AddScoped<ICommandValidator<DeactivateAccountCommand>, DeactivateAccountValidator>();
        services.AddScoped<ICommandHandler<RequestPasswordResetCommand, Unit>, RequestPasswordResetHandler>();
        services.AddScoped<ICommandValidator<RequestPasswordResetCommand>, RequestPasswordResetValidator>();
        services.AddScoped<ICommandHandler<ResetPasswordCommand, Unit>, ResetPasswordHandler>();
        services.AddScoped<ICommandValidator<ResetPasswordCommand>, ResetPasswordValidator>();
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
