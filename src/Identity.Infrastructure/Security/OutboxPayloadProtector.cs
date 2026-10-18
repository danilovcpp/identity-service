using System.Security.Cryptography;
using System.Text;
using Identity.Application.Abstractions.Security;
using Identity.Domain.ValueObjects;

namespace Identity.Infrastructure.Security;

public sealed class OutboxPayloadProtector(OutboxKeyRing keys) : IOutboxPayloadProtector
{
    public EncryptedEmailPayload Encrypt(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        try
        {
            if (bytes.Length is 0 or > 65536) { throw new ArgumentException("The email payload exceeds the supported size."); }
            var nonce = RandomNumberGenerator.GetBytes(12);
            var ciphertext = new byte[bytes.Length];
            var tag = new byte[16];
            using var algorithm = new AesGcm(keys.GetKey(keys.ActiveKeyId), 16);
            algorithm.Encrypt(nonce, bytes, ciphertext, tag, AssociatedData(keys.ActiveKeyId));
            return new EncryptedEmailPayload(keys.ActiveKeyId, Convert.ToBase64String(nonce),
                Convert.ToBase64String(ciphertext), Convert.ToBase64String(tag));
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public string Decrypt(EncryptedEmailPayload payload)
    {
        var ciphertext = Convert.FromBase64String(payload.Ciphertext);
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var algorithm = new AesGcm(keys.GetKey(payload.KeyId), 16);
            algorithm.Decrypt(Convert.FromBase64String(payload.Nonce), ciphertext,
                Convert.FromBase64String(payload.Tag), plaintext, AssociatedData(payload.KeyId));
            return Encoding.UTF8.GetString(plaintext);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static byte[] AssociatedData(string keyId) => Encoding.UTF8.GetBytes($"Identity:email-outbox:v1:{keyId}");
}
