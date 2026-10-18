using System.Text;

namespace Identity.Application.Common;

public static class PasswordText
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string Normalize(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length > 1024 || StrictUtf8.GetByteCount(password) > 1024)
        {
            throw new ArgumentException("Password input exceeds the byte limit.");
        }
        var normalized = password.Normalize(NormalizationForm.FormKC);
        if (StrictUtf8.GetByteCount(normalized) > 512)
        {
            throw new ArgumentException("Normalized password exceeds the byte limit.");
        }
        return normalized;
    }
}
