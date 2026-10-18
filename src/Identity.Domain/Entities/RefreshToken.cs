using Identity.Domain.Common;

namespace Identity.Domain.Entities;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Create(Session session, string tokenHash, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(session);
        DomainRules.Require(session.IsActive(now) && expiresAt <= session.AbsoluteExpiresAt,
            "invalid_session_state", "The token must belong to an active session and cannot outlive it.");
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            TokenHash = DomainRules.TokenHash(tokenHash),
            CreatedAt = now.ToUniversalTime(),
            ExpiresAt = DomainRules.Expiry(now, expiresAt, TimeSpan.FromDays(7))
        };
    }

    public bool IsUsable(DateTimeOffset now) => now >= CreatedAt && now < ExpiresAt && ConsumedAt is null && RevokedAt is null;

    public void Consume(RefreshToken replacement, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        DomainRules.Require(IsUsable(now) && replacement.IsUsable(now)
            && replacement.SessionId == SessionId && replacement.Id != Id && replacement.CreatedAt == now,
            "invalid_token_state", "The refresh token cannot be consumed with this replacement.");
        ConsumedAt = now.ToUniversalTime();
        ReplacedByTokenId = replacement.Id;
    }

    public void Revoke(DateTimeOffset now)
    {
        var timestamp = DomainRules.Time(now, ConsumedAt ?? CreatedAt);
        RevokedAt ??= timestamp;
    }
}
