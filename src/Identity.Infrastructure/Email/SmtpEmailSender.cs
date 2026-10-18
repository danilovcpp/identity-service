using Identity.Application.Abstractions.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Identity.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, Guid messageId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress("Identity", settings.FromAddress));
        mail.To.Add(MailboxAddress.Parse(message.Recipient));
        mail.Subject = message.Subject;
        mail.MessageId = $"{messageId:N}@identity.invalid";
        mail.Body = new TextPart("plain") { Text = message.TextBody };
        using var client = new SmtpClient { Timeout = settings.Smtp.TimeoutSeconds * 1000 };
        var tls = settings.Smtp.Tls switch
        {
            SmtpTlsMode.None => SecureSocketOptions.None,
            SmtpTlsMode.StartTls => SecureSocketOptions.StartTls,
            SmtpTlsMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => throw new InvalidOperationException("Unsupported SMTP TLS mode.")
        };
        // No protocol logger, permissive certificate callbacks or message/body logging.
        await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, tls, cancellationToken);
        if (!string.IsNullOrEmpty(settings.Smtp.UserName))
        {
            await client.AuthenticateAsync(settings.Smtp.UserName, settings.Smtp.Password, cancellationToken);
        }
        await client.SendAsync(mail, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
