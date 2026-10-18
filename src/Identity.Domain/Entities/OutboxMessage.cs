using Identity.Domain.Common;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Entities;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public EncryptedEmailPayload? Payload { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? LastError { get; private set; }

    public static OutboxMessage Create(string type, EncryptedEmailPayload payload, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = DomainRules.Text(type, 80),
            Payload = payload,
            CreatedAt = now.ToUniversalTime(),
            NextAttemptAt = now.ToUniversalTime()
        };
    }

    public void RecordFailure(string errorCode, DateTimeOffset now, DateTimeOffset nextAttemptAt)
    {
        var code = DomainRules.Text(errorCode, 80);
        DomainRules.Require(code.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_'),
            "invalid_error_code", "Only a machine-readable error code can be stored.");
        DomainRules.Require(ProcessedAt is null && AttemptCount < int.MaxValue, "invalid_outbox_state", "The outbox message cannot be retried.");
        DomainRules.Time(now, NextAttemptAt);
        DomainRules.Require(nextAttemptAt > now, "invalid_expiry", "The retry must be scheduled in the future.");
        AttemptCount++;
        LastError = code;
        NextAttemptAt = nextAttemptAt.ToUniversalTime();
    }

    public void MarkProcessed(DateTimeOffset now)
    {
        DomainRules.Require(ProcessedAt is null, "invalid_outbox_state", "The outbox message has already been processed.");
        ProcessedAt = DomainRules.Time(now, NextAttemptAt);
        Payload = null;
        LastError = null;
    }
}
