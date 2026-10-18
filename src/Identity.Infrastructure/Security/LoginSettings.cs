using System.Security.Cryptography;
using Identity.Application.Abstractions.Security;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

internal sealed class LoginSettings(IOptions<JwtOptions> jwt, IOptions<LoginOptions> login, IOptions<IdentityClientOptions> identity) : ILoginSettings
{
    public bool Enabled => jwt.Value.Enabled;
    public string? FindAudience(string clientId) => identity.Value.Clients.SingleOrDefault(client => client.Enabled && client.ClientId == clientId)?.Audience;
    public int FailureLimit => login.Value.FailureLimit;
    public TimeSpan LockoutDuration => TimeSpan.FromSeconds(login.Value.LockoutSeconds);
    public TimeSpan RefreshLifetime => TimeSpan.FromSeconds(login.Value.RefreshLifetimeSeconds);
    public TimeSpan SessionLifetime => TimeSpan.FromSeconds(login.Value.SessionLifetimeSeconds);
}

internal sealed class LoginPasswordVerifier : ILoginPasswordVerifier
{
    private readonly PasswordService _passwords;
    private readonly string _dummyHash;

    public LoginPasswordVerifier(IOptions<PasswordHashOptions> options)
    {
        _passwords = new PasswordService(options);
        _dummyHash = _passwords.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public PasswordVerificationResult Verify(string? passwordHash, string password) => _passwords.Verify(passwordHash ?? _dummyHash, password);
}
