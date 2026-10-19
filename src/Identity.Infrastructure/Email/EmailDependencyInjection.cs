using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Infrastructure.Persistence.Repositories;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Email;

internal static class EmailDependencyInjection
{
    public static IServiceCollection AddRegistrationAndEmail(this IServiceCollection services)
    {
        services.AddOptions<RegistrationOptions>().BindConfiguration("Registration").ValidateOnStart();
        services.AddOptions<PasswordResetOptions>().BindConfiguration("PasswordReset").ValidateOnStart();
        services.AddSingleton<IValidateOptions<PasswordResetOptions>, PasswordResetOptionsValidator>();
        services.AddSingleton<IPasswordResetSettings>(provider => provider.GetRequiredService<IOptions<PasswordResetOptions>>().Value);
        services.AddOptions<PasswordHashOptions>().BindConfiguration("PasswordHash")
            .Validate(options => options.IterationCount is >= 600_000 and <= 2_000_000, "Password hashing iterations must be between 600000 and 2000000.").ValidateOnStart();
        services.AddOptions<OutboxOptions>().BindConfiguration("Outbox")
            .Validate(options => options.BatchSize is >= 1 and <= 100 && options.MaxAttempts is >= 1 and <= 20
                && options.PollSeconds is >= 0.05 and <= 60 && options.BaseRetrySeconds is >= 0.1 and <= 300,
                "Email outbox limits are invalid.").ValidateOnStart();
        services.AddOptions<EmailOptions>().BindConfiguration("Email")
            .Validate<IOptions<RegistrationOptions>, IOptions<OutboxOptions>, IOptions<PasswordResetOptions>, IServiceProvider>((options, registration, outbox, reset, provider) =>
                (!registration.Value.Enabled && !outbox.Value.Enabled && !reset.Value.Enabled)
                || EmailOptions.IsValid(options, provider.GetService<IHostEnvironment>()?.IsProduction() == true,
                    requireVerification: registration.Value.Enabled, requireReset: reset.Value.Enabled),
                "Email delivery configuration is missing or insecure.").ValidateOnStart();
        services.AddOptions<OutboxEncryptionOptions>().BindConfiguration("OutboxEncryption")
            .Validate<IOptions<RegistrationOptions>, IOptions<OutboxOptions>, IOptions<PasswordResetOptions>, IServiceProvider>((options, registration, outbox, reset, provider) =>
                (!registration.Value.Enabled && !outbox.Value.Enabled && !reset.Value.Enabled) || (OutboxEncryptionOptions.IsValid(options)
                    && (provider.GetService<IHostEnvironment>()?.IsProduction() != true || options.Keys.Keys.All(key =>
                        !key.StartsWith("dev-", StringComparison.OrdinalIgnoreCase) && !key.StartsWith("test-", StringComparison.OrdinalIgnoreCase)))),
                "An active outbox encryption key and absolute secret file paths are required.").ValidateOnStart();
        services.AddSingleton<IValidateOptions<RegistrationOptions>, RegistrationOptionsValidator>();
        services.AddSingleton<IRegistrationSettings>(provider => provider.GetRequiredService<IOptions<RegistrationOptions>>().Value);
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenGenerator, TokenGenerator>();
        services.AddSingleton<OutboxKeyRing>();
        services.AddHostedService(provider => provider.GetRequiredService<OutboxKeyRing>());
        services.AddSingleton<IOutboxPayloadProtector, OutboxPayloadProtector>();
        services.AddSingleton<IVerificationEmailFactory, VerificationEmailFactory>();
        services.AddSingleton<IPasswordResetEmailFactory, PasswordResetEmailFactory>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
        services.AddScoped<IEmailVerificationRepository, EmailVerificationRepository>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<EmailOutboxProcessor>();
        services.AddSingleton<EmailOutboxMetrics>();
        services.AddHostedService<EmailOutboxWorker>();
        return services;
    }

    private sealed class RegistrationOptionsValidator(IOptions<OutboxOptions> outbox, IServiceProvider provider) : IValidateOptions<RegistrationOptions>
    {
        public ValidateOptionsResult Validate(string? name, RegistrationOptions options) => options.Enabled
            && provider.GetService<IHostEnvironment>()?.IsProduction() == true && !outbox.Value.Enabled
                ? ValidateOptionsResult.Fail("Production registration requires the email outbox worker.")
                : ValidateOptionsResult.Success;
    }

    private sealed class PasswordResetOptionsValidator(IOptions<OutboxOptions> outbox, IServiceProvider provider) : IValidateOptions<PasswordResetOptions>
    {
        public ValidateOptionsResult Validate(string? name, PasswordResetOptions options) => options.Enabled
            && provider.GetService<IHostEnvironment>()?.IsProduction() == true && !outbox.Value.Enabled
                ? ValidateOptionsResult.Fail("Production password reset requires the email outbox worker.")
                : ValidateOptionsResult.Success;
    }
}
