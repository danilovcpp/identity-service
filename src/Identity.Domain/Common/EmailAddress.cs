using System.Net.Mail;

namespace Identity.Domain.Common;

public static class EmailAddress
{
    public static string Canonicalize(string email)
    {
        DomainRules.Text(email, 320);
        var trimmed = email.Trim();
        DomainRules.Require(MailAddress.TryCreate(trimmed, out var address)
            && string.Equals(address.Address, trimmed, StringComparison.Ordinal),
            "invalid_email", "The email address is invalid.");
        return trimmed;
    }

    public static string Normalize(string email) => Canonicalize(email).ToUpperInvariant();
}
