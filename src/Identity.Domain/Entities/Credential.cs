using Identity.Domain.Common;

namespace Identity.Domain.Entities;

public sealed class Credential
{
    private Credential() { }

    public Guid UserId { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset PasswordChangedAt { get; private set; }

    public static Credential Create(Guid userId, string passwordHash, DateTimeOffset now) => new()
    {
        UserId = DomainRules.Identifier(userId),
        PasswordHash = DomainRules.Text(passwordHash, 4096),
        CreatedAt = now.ToUniversalTime(),
        PasswordChangedAt = now.ToUniversalTime()
    };

    public void ChangePasswordHash(string passwordHash, DateTimeOffset now)
    {
        var hash = DomainRules.Text(passwordHash, 4096);
        var timestamp = DomainRules.Time(now, PasswordChangedAt);
        PasswordHash = hash;
        PasswordChangedAt = timestamp;
    }

    public void Rehash(string passwordHash) => PasswordHash = DomainRules.Text(passwordHash, 4096);
}
