namespace Identity.Application.Common;

public static class OpaqueTokenText
{
    public static bool IsSupported(string? value) => value is { Length: >= 43 and <= 512 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
