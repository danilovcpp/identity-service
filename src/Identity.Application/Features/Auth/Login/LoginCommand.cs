using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Auth.Login;

public sealed class LoginCommand(string email, string password, string clientId, string? userAgent) : ICommand<LoginResult>
{
    public string Email { get; } = email;
    public string Password { get; } = password;
    public string ClientId { get; } = clientId;
    public string? UserAgent { get; } = userAgent;
}

public sealed class LoginResult(string accessToken, string refreshToken, int expiresIn)
{
    public string AccessToken { get; } = accessToken;
    public string RefreshToken { get; } = refreshToken;
    public string TokenType => "Bearer";
    public int ExpiresIn { get; } = expiresIn;
}
