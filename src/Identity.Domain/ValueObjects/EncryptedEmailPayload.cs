using Identity.Domain.Common;

namespace Identity.Domain.ValueObjects;

// Carries an AES-256-GCM envelope only. Encryption and key handling belong to Infrastructure.
public sealed class EncryptedEmailPayload
{
    public EncryptedEmailPayload(string keyId, string nonce, string ciphertext, string tag)
    {
        KeyId = DomainRules.Text(keyId, 100);
        DomainRules.Require(keyId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'),
            "invalid_key_id", "The encryption key identifier is invalid.");
        Nonce = ValidateBase64(nonce, 12, 12);
        Ciphertext = ValidateBase64(ciphertext, 1, 65536);
        Tag = ValidateBase64(tag, 16, 16);
    }

    public string KeyId { get; }
    public string Nonce { get; }
    public string Ciphertext { get; }
    public string Tag { get; }

    private static string ValidateBase64(string value, int minimumBytes, int maximumBytes)
    {
        DomainRules.Require(value is { Length: > 0 } && value.Length <= ((maximumBytes + 2) / 3) * 4,
            "invalid_encrypted_payload", "The encrypted payload is invalid.");
        var buffer = new byte[maximumBytes];
        DomainRules.Require(Convert.TryFromBase64String(value, buffer, out var length) && length >= minimumBytes,
            "invalid_encrypted_payload", "The encrypted payload is invalid.");
        return value;
    }
}
