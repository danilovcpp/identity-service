using System.Text.Json;
using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Email;

public sealed class VerificationEmailFactory(IOutboxPayloadProtector protector, IOptions<EmailOptions> options) : IVerificationEmailFactory
{
    public OutboxMessage Create(string email, string token, DateTimeOffset now)
    {
        var link = options.Value.VerificationUrlTemplate.Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);
        var message = new EmailMessage(email, "Verify your Identity email",
            $"Welcome to Identity.\n\nConfirm your email address using this link:\n{link}\n\nThis link expires in 24 hours. If you did not request this, ignore this message.");
        var payload = protector.Encrypt(JsonSerializer.Serialize(message));
        return OutboxMessage.Create("email_verification", payload, now);
    }
}
