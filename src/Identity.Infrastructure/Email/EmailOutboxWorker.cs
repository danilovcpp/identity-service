using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Email;

public sealed class EmailOutboxWorker(IServiceScopeFactory scopes, IOptions<OutboxOptions> options,
    TimeProvider clock, ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var index = 0; index < options.Value.BatchSize; index++)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    if (!await scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().ProcessNextAsync(stoppingToken)) { break; }
                }
                await using var metricsScope = scopes.CreateAsyncScope();
                await metricsScope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().RefreshMetricsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError("Email outbox iteration failed with type {ExceptionType}", exception.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), clock, stoppingToken);
        }
    }
}
