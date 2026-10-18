using Identity.Domain.Common;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class Session
{
    private Session() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string ClientId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastUsedAt { get; private set; }
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public SessionRevokeReason? RevokeReason { get; private set; }
    public string? UserAgent { get; private set; }

    public static Session Create(Guid userId, string clientId, string? userAgent, DateTimeOffset now, DateTimeOffset absoluteExpiresAt)
    {
        var sanitizedAgent = userAgent is null ? null : new string(userAgent.Where(character => !char.IsControl(character)).Take(512).ToArray());
        return new Session
        {
            Id = Guid.NewGuid(),
            UserId = DomainRules.Identifier(userId),
            ClientId = DomainRules.Text(clientId, 100),
            UserAgent = sanitizedAgent,
            CreatedAt = now.ToUniversalTime(),
            LastUsedAt = now.ToUniversalTime(),
            AbsoluteExpiresAt = DomainRules.Expiry(now, absoluteExpiresAt, TimeSpan.FromDays(30))
        };
    }

    public bool IsActive(DateTimeOffset now) => now >= CreatedAt && now < AbsoluteExpiresAt && RevokedAt is null;

    public void Touch(DateTimeOffset now)
    {
        DomainRules.Require(IsActive(now), "invalid_session_state", "The session is not active.");
        LastUsedAt = DomainRules.Time(now, LastUsedAt);
    }

    public void Revoke(SessionRevokeReason reason, DateTimeOffset now)
    {
        DomainRules.Require(Enum.IsDefined(reason), "invalid_revoke_reason", "The revocation reason is invalid.");
        var timestamp = DomainRules.Time(now, LastUsedAt);
        if (RevokedAt is null)
        {
            RevokedAt = timestamp;
            RevokeReason = reason;
        }
    }
}
