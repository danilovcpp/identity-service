using Identity.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Maintenance;

public sealed class SessionCleanupWorker(IServiceScopeFactory scopes, IOptions<SessionCleanupOptions> options,
    IOptions<JwtOptions> jwt, TimeProvider clock, ILogger<SessionCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !jwt.Value.Enabled) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SessionCleanupProcessor>().ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError("Session cleanup failed with type {ExceptionType}", exception.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), clock, stoppingToken);
        }
    }
}
