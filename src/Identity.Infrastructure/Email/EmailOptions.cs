using System.Net.Mail;

namespace Identity.Infrastructure.Email;

public sealed class EmailOptions
{
    public string FromAddress { get; set; } = string.Empty;
    public string VerificationUrlTemplate { get; set; } = string.Empty;
    public string ResetPasswordUrlTemplate { get; set; } = string.Empty;
    public SmtpOptions Smtp { get; set; } = new();

    public static bool IsValidTemplate(string template, bool production)
    {
        var marker = template.IndexOf("{token}", StringComparison.Ordinal);
        if (marker < 0 || template.LastIndexOf("{token}", StringComparison.Ordinal) != marker
            || template.Replace("{token}", "", StringComparison.Ordinal).Any(character => character is '{' or '}' || char.IsControl(character)))
        {
            return false;
        }
        return Uri.TryCreate(template.Replace("{token}", "one-time-token", StringComparison.Ordinal), UriKind.Absolute, out var uri)
            && string.IsNullOrEmpty(uri.UserInfo) && (uri.Scheme == "https" || (!production && uri.Scheme == "http" && uri.IsLoopback));
    }

    public static bool IsValid(EmailOptions options, bool production, bool requireVerification = true, bool requireReset = false) =>
        (!requireVerification || IsValidTemplate(options.VerificationUrlTemplate, production))
            && (!requireReset || IsValidTemplate(options.ResetPasswordUrlTemplate, production))
            && MailAddress.TryCreate(options.FromAddress, out var address) && address.Address == options.FromAddress
            && !options.FromAddress.Any(char.IsControl)
            && !string.IsNullOrWhiteSpace(options.Smtp.Host) && !options.Smtp.Host.Any(char.IsControl)
            && options.Smtp.Port is > 0 and <= 65535 && options.Smtp.TimeoutSeconds is >= 1 and <= 60
            && Enum.IsDefined(options.Smtp.Tls) && (!production || options.Smtp.Tls != SmtpTlsMode.None)
            && (string.IsNullOrEmpty(options.Smtp.UserName) == string.IsNullOrEmpty(options.Smtp.Password));
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public SmtpTlsMode Tls { get; set; } = SmtpTlsMode.StartTls;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 10;
}

public enum SmtpTlsMode { None, StartTls, SslOnConnect }
