using Identity.Domain.Common;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Entities;

public sealed class SecurityAuditEvent
{
    private SecurityAuditEvent() { }

    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string EventType { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public string? ClientId { get; private set; }
    public string CorrelationId { get; private set; } = null!;
    public AuditMetadata Metadata { get; private set; } = null!;

    public static SecurityAuditEvent Create(SecurityEventType eventType, Guid? userId, string? clientId,
        string correlationId, AuditMetadata metadata, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var name = eventType switch
        {
            SecurityEventType.Register => "register",
            SecurityEventType.VerifyEmail => "verify_email",
            SecurityEventType.LoginSuccess => "login_success",
            SecurityEventType.LoginFailed => "login_failed",
            SecurityEventType.RefreshRotated => "refresh_rotated",
            SecurityEventType.RefreshReplayDetected => "refresh_replay_detected",
            SecurityEventType.Logout => "logout",
            SecurityEventType.LogoutAll => "logout_all",
            SecurityEventType.PasswordChanged => "password_changed",
            SecurityEventType.PasswordReset => "password_reset",
            SecurityEventType.AccountDeactivated => "account_deactivated",
            _ => throw new ArgumentOutOfRangeException(nameof(eventType))
        };
        return new SecurityAuditEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId is null ? null : DomainRules.Identifier(userId.Value),
            EventType = name,
            OccurredAt = now.ToUniversalTime(),
            ClientId = clientId is null ? null : DomainRules.Text(clientId, 100),
            CorrelationId = DomainRules.Text(correlationId, 128),
            Metadata = metadata
        };
    }
}
