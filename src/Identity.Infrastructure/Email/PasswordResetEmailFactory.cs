using System.Text.Json;
using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Email;

public sealed class PasswordResetEmailFactory(IOutboxPayloadProtector protector, IOptions<EmailOptions> options) : IPasswordResetEmailFactory
{
    public OutboxMessage Create(string email, string token, DateTimeOffset now)
    {
        var link = options.Value.ResetPasswordUrlTemplate.Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);
        var message = new EmailMessage(email, "Reset your Identity password",
            $"A password reset was requested for your Identity account.\n\nChoose a new password using this link:\n{link}\n\nThis link expires in 30 minutes. If you did not request this, ignore this message.");
        return OutboxMessage.Create("password_reset", protector.Encrypt(JsonSerializer.Serialize(message)), now);
    }
}
