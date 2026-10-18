using Identity.Application.Abstractions.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Security;

public sealed class JwtTokenService(JwtKeyRing keys, IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public Task<IssuedAccessToken> IssueAsync(AccessTokenSubject subject, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds());
        var expires = now.AddSeconds(options.Value.AccessTokenLifetimeSeconds);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Value.Issuer,
            Audience = subject.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = keys.SigningCredentials,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = subject.UserId.ToString(),
                ["sid"] = subject.SessionId.ToString(),
                ["client_id"] = subject.ClientId,
                ["jti"] = Guid.NewGuid().ToString("N"),
                ["token_use"] = "access"
            }
        };
        return Task.FromResult(new IssuedAccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires, options.Value.AccessTokenLifetimeSeconds));
    }
}
