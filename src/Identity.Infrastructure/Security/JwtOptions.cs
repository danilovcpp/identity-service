namespace Identity.Infrastructure.Security;

public sealed class JwtOptions
{
    public bool Enabled { get; set; }
    public string Issuer { get; set; } = "";
    public int AccessTokenLifetimeSeconds { get; set; } = 900;
    public int ClockSkewSeconds { get; set; } = 30;
    public string ActiveKeyId { get; set; } = "";
    public string PrivateKeyPath { get; set; } = "";
    public Dictionary<string, string> PublicKeys { get; set; } = new(StringComparer.Ordinal);

    public static bool IsKeyId(string value) => value.Length is >= 1 and <= 100
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public static bool IsValid(JwtOptions options, bool production) => !options.Enabled ||
        (Uri.TryCreate(options.Issuer, UriKind.Absolute, out var issuer) && issuer.Scheme == Uri.UriSchemeHttps
         && string.IsNullOrEmpty(issuer.UserInfo) && string.IsNullOrEmpty(issuer.Query) && string.IsNullOrEmpty(issuer.Fragment)
         && options.AccessTokenLifetimeSeconds is >= 60 and <= 900 && options.ClockSkewSeconds is >= 0 and <= 30
         && IsKeyId(options.ActiveKeyId) && Path.IsPathFullyQualified(options.PrivateKeyPath)
         && options.PublicKeys.Count <= 10 && options.PublicKeys.All(pair => IsKeyId(pair.Key)
             && pair.Key != options.ActiveKeyId && Path.IsPathFullyQualified(pair.Value))
         && (!production || options.PublicKeys.Keys.Append(options.ActiveKeyId).All(key =>
             !key.StartsWith("dev-", StringComparison.OrdinalIgnoreCase) && !key.StartsWith("test-", StringComparison.OrdinalIgnoreCase))));
}

public sealed class IdentityClientOptions
{
    public List<IdentityClient> Clients { get; set; } = [];

    public static bool IsValid(IdentityClientOptions options) => options.Clients.Count is >= 1 and <= 100
        && options.Clients.Any(client => client.Enabled)
        && options.Clients.Select(client => client.ClientId).Distinct(StringComparer.Ordinal).Count() == options.Clients.Count
        && options.Clients.All(client => !string.IsNullOrWhiteSpace(client.ClientId) && client.ClientId.Length <= 100
            && !client.ClientId.Any(char.IsControl) && !string.IsNullOrWhiteSpace(client.Audience)
            && client.Audience.Length <= 300 && !client.Audience.Any(char.IsControl));
}

public sealed class IdentityClient
{
    public string ClientId { get; set; } = "";
    public string Audience { get; set; } = "";
    public bool Enabled { get; set; }
}

public sealed class LoginOptions
{
    public int FailureLimit { get; set; } = 5;
    public int LockoutSeconds { get; set; } = 900;
    public int RefreshLifetimeSeconds { get; set; } = 604800;
    public int SessionLifetimeSeconds { get; set; } = 2592000;
}
