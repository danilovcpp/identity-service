using Identity.Domain.Exceptions;

namespace Identity.Domain.Common;

internal static class DomainRules
{
    public static void Require(bool condition, string code, string message)
    {
        if (!condition)
        {
            throw new DomainException(code, message);
        }
    }

    public static Guid Identifier(Guid value)
    {
        Require(value != Guid.Empty, "invalid_identifier", "An identifier is required.");
        return value;
    }

    public static string Text(string value, int maximumLength)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength && !value.Any(char.IsControl),
            "invalid_text", "Text is empty, too long or contains control characters.");
        return value;
    }

    public static string TokenHash(string value)
    {
        Require(value is { Length: 64 } && value.All(char.IsAsciiHexDigit), "invalid_token_hash", "A SHA-256 token hash is required.");
        return value.ToLowerInvariant();
    }

    public static DateTimeOffset Time(DateTimeOffset value, DateTimeOffset minimum)
    {
        Require(value >= minimum, "invalid_time", "Time cannot precede the previous state.");
        return value.ToUniversalTime();
    }

    public static DateTimeOffset Expiry(DateTimeOffset now, DateTimeOffset expiry, TimeSpan maximumLifetime)
    {
        Require(expiry > now && expiry - now <= maximumLifetime, "invalid_expiry", "The expiry is outside the allowed lifetime.");
        return expiry.ToUniversalTime();
    }
}
