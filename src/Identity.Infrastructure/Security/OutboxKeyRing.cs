using System.Security.Cryptography;
using Identity.Infrastructure.Email;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

public sealed class OutboxKeyRing(IOptions<OutboxEncryptionOptions> options,
    IOptions<RegistrationOptions> registration, IOptions<OutboxOptions> outbox, IOptions<PasswordResetOptions> reset) : IHostedService, IDisposable
{
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);
    public string ActiveKeyId => options.Value.ActiveKeyId;

    internal byte[] GetKey(string keyId) => _keys.TryGetValue(keyId, out var key)
        ? key : throw new CryptographicException("The outbox encryption key is unavailable.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!registration.Value.Enabled && !outbox.Value.Enabled && !reset.Value.Enabled) { return; }
        try
        {
            foreach (var pair in options.Value.Keys)
            {
                var key = await File.ReadAllBytesAsync(pair.Value, cancellationToken);
                if (key.Length != 32 || key.All(value => value == 0))
                {
                    CryptographicOperations.ZeroMemory(key);
                    throw new CryptographicException();
                }
                _keys.Add(pair.Key, key);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { Dispose(); throw; }
        catch (Exception)
        {
            Dispose();
            throw new InvalidOperationException("Outbox encryption keys could not be loaded. Check secret mounts and 32-byte key material.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public void Dispose()
    {
        foreach (var key in _keys.Values) { CryptographicOperations.ZeroMemory(key); }
        _keys.Clear();
    }
}
