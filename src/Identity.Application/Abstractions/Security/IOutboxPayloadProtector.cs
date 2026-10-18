using Identity.Domain.ValueObjects;

namespace Identity.Application.Abstractions.Security;

public interface IOutboxPayloadProtector
{
    EncryptedEmailPayload Encrypt(string plaintext);
    string Decrypt(EncryptedEmailPayload payload);
}
