namespace Identity.Infrastructure.Security;

public sealed class OutboxEncryptionOptions
{
    public string ActiveKeyId { get; set; } = string.Empty;
    public Dictionary<string, string> Keys { get; set; } = new(StringComparer.Ordinal);

    public static bool IsValid(OutboxEncryptionOptions options) => options.Keys.Count is > 0 and <= 10
        && options.Keys.ContainsKey(options.ActiveKeyId)
        && options.Keys.All(pair => pair.Key.Length is > 0 and <= 100
            && pair.Key.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            && Path.IsPathFullyQualified(pair.Value));
}
