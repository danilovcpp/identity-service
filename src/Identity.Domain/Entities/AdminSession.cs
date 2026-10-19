using Identity.Domain.Common;

namespace Identity.Domain.Entities;

public sealed class AdminSession
{
    private AdminSession() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static AdminSession Create(Guid userId, DateTimeOffset now, DateTimeOffset expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = DomainRules.Identifier(userId),
        CreatedAt = now.ToUniversalTime(),
        LastSeenAt = now.ToUniversalTime(),
        ExpiresAt = DomainRules.Expiry(now, expiresAt, TimeSpan.FromHours(8))
    };

    public bool IsActive(DateTimeOffset now, TimeSpan idleTimeout) => RevokedAt is null && now >= LastSeenAt
        && now < ExpiresAt && now - LastSeenAt < idleTimeout;

    public void Touch(DateTimeOffset now, TimeSpan idleTimeout)
    {
        DomainRules.Require(IsActive(now, idleTimeout), "invalid_admin_session", "The administrator session has expired.");
        LastSeenAt = now.ToUniversalTime();
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= DomainRules.Time(now, CreatedAt);
}
