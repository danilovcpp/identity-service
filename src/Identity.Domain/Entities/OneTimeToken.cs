using Identity.Domain.Common;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class OneTimeToken
{
    private OneTimeToken() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public OneTimeTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public static OneTimeToken Create(Guid userId, OneTimeTokenPurpose purpose, string tokenHash, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        DomainRules.Require(Enum.IsDefined(purpose), "invalid_token_purpose", "The token purpose is invalid.");
        var maximumLifetime = purpose switch
        {
            OneTimeTokenPurpose.EmailVerification => TimeSpan.FromHours(24),
            OneTimeTokenPurpose.Invitation => TimeSpan.FromHours(72),
            _ => TimeSpan.FromMinutes(30)
        };
        return new OneTimeToken
        {
            Id = Guid.NewGuid(),
            UserId = DomainRules.Identifier(userId),
            Purpose = purpose,
            TokenHash = DomainRules.TokenHash(tokenHash),
            CreatedAt = now.ToUniversalTime(),
            ExpiresAt = DomainRules.Expiry(now, expiresAt, maximumLifetime)
        };
    }

    public bool IsUsable(DateTimeOffset now) => now >= CreatedAt && now < ExpiresAt && ConsumedAt is null;

    public void Consume(DateTimeOffset now)
    {
        DomainRules.Require(IsUsable(now), "invalid_token_state", "The one-time token is invalid.");
        ConsumedAt = now.ToUniversalTime();
    }
}
