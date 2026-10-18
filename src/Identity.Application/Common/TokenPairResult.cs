namespace Identity.Application.Common;

// Shared login/refresh response; never print bearer secrets through a record-generated ToString.
public sealed class TokenPairResult(string accessToken, string refreshToken, int expiresIn)
{
    public string AccessToken { get; } = accessToken;
    public string RefreshToken { get; } = refreshToken;
    public string TokenType => "Bearer";
    public int ExpiresIn { get; } = expiresIn;
}
