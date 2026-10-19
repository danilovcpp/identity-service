using System.Security.Cryptography;
using System.Text.Json;
using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Security;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Email;

public sealed class EmailOutboxProcessor(IdentityDbContext context, IEmailSender sender,
    IOutboxPayloadProtector protector, IOptions<OutboxOptions> options, TimeProvider clock,
    EmailOutboxMetrics metrics, ILogger<EmailOutboxProcessor> logger)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var candidates = await context.OutboxMessages.FromSqlInterpolated($"SELECT *, xmin FROM outbox_messages WHERE processed_at IS NULL AND attempt_count < {options.Value.MaxAttempts} AND next_attempt_at <= {now} ORDER BY next_attempt_at, id LIMIT 1 FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);
        var message = candidates.SingleOrDefault();
        if (message is null) { return false; }
        try
        {
            if (message.Type is not ("email_verification" or "password_reset")) { throw new CryptographicException("Unsupported message type."); }
            var email = JsonSerializer.Deserialize<EmailMessage>(protector.Decrypt(message.Payload!))
                ?? throw new CryptographicException("Invalid email envelope.");
            await sender.SendAsync(email, message.Id, cancellationToken);
            message.MarkProcessed(clock.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Email outbox delivery failed with type {ExceptionType}", exception.GetType().Name);
            var timestamp = clock.GetUtcNow();
            var delay = Math.Min(21_600, options.Value.BaseRetrySeconds * Math.Pow(2, Math.Min(message.AttemptCount, 16)));
            message.RecordFailure(exception is CryptographicException or JsonException ? "payload_unavailable" : "smtp_unavailable",
                timestamp, timestamp.AddSeconds(delay));
            metrics.Failed();
            if (message.AttemptCount >= options.Value.MaxAttempts)
            {
                logger.LogError("Email outbox message {MessageId} exhausted {AttemptCount} attempts", message.Id, message.AttemptCount);
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (message.ProcessedAt is not null) { metrics.Delivered(); }
        return true;
    }

    public async Task RefreshMetricsAsync(CancellationToken cancellationToken)
    {
        var pending = await context.OutboxMessages.AsNoTracking().LongCountAsync(message => message.ProcessedAt == null, cancellationToken);
        var failed = await context.OutboxMessages.AsNoTracking().LongCountAsync(message => message.ProcessedAt == null
            && message.AttemptCount >= options.Value.MaxAttempts, cancellationToken);
        metrics.Backlog(pending, failed);
    }
}
