namespace Identity.Application.Abstractions.Notifications;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, Guid messageId, CancellationToken cancellationToken);
}

// Ordinary classes deliberately avoid generated ToString methods containing email/token data.
public sealed class EmailMessage(string recipient, string subject, string textBody)
{
    public string Recipient { get; } = recipient;
    public string Subject { get; } = subject;
    public string TextBody { get; } = textBody;
}
