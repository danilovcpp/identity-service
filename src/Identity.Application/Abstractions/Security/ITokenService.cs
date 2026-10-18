namespace Identity.Application.Abstractions.Security;

public interface ITokenService
{
    Task<IssuedAccessToken> IssueAsync(AccessTokenSubject subject, CancellationToken cancellationToken);
}

public sealed record AccessTokenSubject(Guid UserId, Guid SessionId, string ClientId, string Audience);

// Avoid a record-generated ToString containing the bearer token.
public sealed class IssuedAccessToken(string token, DateTimeOffset expiresAt, int lifetimeSeconds)
{
    public string Token { get; } = token;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public int LifetimeSeconds { get; } = lifetimeSeconds;
}
