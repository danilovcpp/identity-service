using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Security;

// Keys are immutable for a process lifetime; rotation is a deliberate configuration + restart operation.
public sealed class JwtKeyRing(IOptions<JwtOptions> options) : IHostedService, IDisposable
{
    // IdentityModel's global cache can retain providers backed by a disposed RSA after an in-process host restart.
    private readonly CryptoProviderFactory _crypto = new() { CacheSignatureProviders = false };
    private readonly Dictionary<string, RsaSecurityKey> _publicKeys = new(StringComparer.Ordinal);
    private readonly List<RSA> _keys = [];
    private SigningCredentials? _signing;
    public SigningCredentials SigningCredentials => _signing ?? throw new InvalidOperationException("JWT signing is unavailable.");

    public IReadOnlyCollection<PublicJwk> GetPublicKeys() => _publicKeys.Select(pair =>
    {
        var parameters = pair.Value.Rsa.ExportParameters(false);
        return new PublicJwk("RSA", "sig", "RS256", pair.Key,
            Base64UrlEncoder.Encode(parameters.Modulus!), Base64UrlEncoder.Encode(parameters.Exponent!));
    }).ToArray();

    public IEnumerable<SecurityKey> Resolve(string? keyId) => keyId is not null && _publicKeys.TryGetValue(keyId, out var key) ? [key] : [];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) { return; }
        try
        {
            var active = await ReadKeyAsync(options.Value.PrivateKeyPath, true, cancellationToken);
            // Exporting private parameters checks that this is actually a signing key.
            var privateParameters = active.ExportParameters(true);
            ClearPrivateParameters(privateParameters);
            _signing = new SigningCredentials(new RsaSecurityKey(active) { KeyId = options.Value.ActiveKeyId, CryptoProviderFactory = _crypto }, SecurityAlgorithms.RsaSha256);
            var publicActive = RSA.Create();
            _keys.Add(publicActive);
            publicActive.ImportParameters(active.ExportParameters(false));
            _publicKeys.Add(options.Value.ActiveKeyId, new RsaSecurityKey(publicActive) { KeyId = options.Value.ActiveKeyId, CryptoProviderFactory = _crypto });
            foreach (var pair in options.Value.PublicKeys)
            {
                var key = await ReadKeyAsync(pair.Value, false, cancellationToken);
                _publicKeys.Add(pair.Key, new RsaSecurityKey(key) { KeyId = pair.Key, CryptoProviderFactory = _crypto });
            }
        }
        catch (OperationCanceledException) { Dispose(); throw; }
        catch (Exception)
        {
            Dispose();
            // File paths, PEM content and inner exception messages must never reach logs.
            throw new InvalidOperationException("JWT key configuration could not be loaded.");
        }
    }

    private async Task<RSA> ReadKeyAsync(string path, bool privateKey, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (info.Length is < 1 or > 32768) { throw new CryptographicException(); }
        var pem = await File.ReadAllTextAsync(path, cancellationToken);
        if (!privateKey && (pem.Contains("PRIVATE", StringComparison.Ordinal) || !pem.Contains("PUBLIC KEY", StringComparison.Ordinal)))
        {
            throw new CryptographicException();
        }
        var key = RSA.Create();
        _keys.Add(key);
        key.ImportFromPem(pem);
        if (key.KeySize < 2048) { throw new CryptographicException(); }
        return key;
    }

    private static void ClearPrivateParameters(RSAParameters parameters)
    {
        foreach (var bytes in new[] { parameters.D, parameters.P, parameters.Q, parameters.DP, parameters.DQ, parameters.InverseQ })
        {
            if (bytes is not null) { CryptographicOperations.ZeroMemory(bytes); }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public void Dispose()
    {
        _signing = null;
        _publicKeys.Clear();
        foreach (var key in _keys) { key.Dispose(); }
        _keys.Clear();
    }
}

public sealed record PublicJwk(string Kty, string Use, string Alg, string Kid, string N, string E);
