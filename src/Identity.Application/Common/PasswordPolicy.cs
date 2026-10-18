using System.Text;
using Identity.Application.Abstractions.Security;

namespace Identity.Application.Common;

public sealed class PasswordPolicy : IPasswordPolicy
{
    // Local common-password denylist. These are public examples, never user passwords.
    private static readonly HashSet<string> Denied = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "123456", "123456789", "qwerty", "letmein", "admin", "welcome", "password123",
        "passwordpassword", "passwordpassword1", "password123456789", "123456789012345", "1234567890123456",
        "12345678901234567890", "qwertyuiopasdfgh", "qwertyuiopasdfghjkl", "letmeinletmein123",
        "iloveyouiloveyou", "111111111111111", "000000000000000", "aaaaaaaaaaaaaaa", "correcthorsebatterystaple"
    };

    public IReadOnlyList<FieldValidationError> Validate(string password)
    {
        try
        {
            var normalized = PasswordText.Normalize(password);
            var length = normalized.EnumerateRunes().Count();
            if (length is < 15 or > 128)
            {
                return [new("password", "password_length", "Use between 15 and 128 Unicode characters.")];
            }
            if (Denied.Contains(normalized))
            {
                return [new("password", "password_common", "Choose a less common password.")];
            }
            return [];
        }
        catch (ArgumentException)
        {
            return [new("password", "password_invalid", "The password has invalid Unicode or exceeds the byte limit.")];
        }
    }
}
