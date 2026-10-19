using System.Globalization;
using System.Text.Json;
using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Email;

public sealed class InvitationEmailFactory(IOutboxPayloadProtector protector, IOptions<EmailOptions> options,
    IInvitationSettings settings) : IInvitationEmailFactory
{
    public OutboxMessage Create(string email, string token, DateTimeOffset now)
    {
        var link = options.Value.InvitationUrlTemplate.Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);
        var expiry = (now + settings.Lifetime).ToUniversalTime().ToString("u", CultureInfo.InvariantCulture);
        var message = new EmailMessage(email, "Your Identity invitation",
            $"You have been invited to create an Identity account.\n\nChoose your password and confirm your email using this link:\n{link}\n\nThis single-use invitation expires at {expiry}. If you did not expect this invitation, ignore this message.");
        return OutboxMessage.Create("invitation", protector.Encrypt(JsonSerializer.Serialize(message)), now);
    }
}
