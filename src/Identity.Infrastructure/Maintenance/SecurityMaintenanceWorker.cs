using Identity.Infrastructure.Email;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Maintenance;

public sealed class SecurityMaintenanceWorker(IServiceScopeFactory scopes, IOptions<SecurityMaintenanceOptions> options,
    IOptions<JwtOptions> jwt, IOptions<RegistrationOptions> registration, IOptions<PasswordResetOptions> reset,
    IOptions<OutboxOptions> outbox, TimeProvider clock, ILogger<SecurityMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || (!jwt.Value.Enabled && !registration.Value.Enabled && !reset.Value.Enabled && !outbox.Value.Enabled)) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SecurityMaintenanceProcessor>().ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError("Security maintenance failed with type {ExceptionType}", exception.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), clock, stoppingToken);
        }
    }
}
