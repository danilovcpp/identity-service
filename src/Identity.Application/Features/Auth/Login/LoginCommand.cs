using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;

namespace Identity.Application.Features.Auth.Login;

public sealed class LoginCommand(string email, string password, string clientId, string? userAgent) : ICommand<TokenPairResult>
{
    public string Email { get; } = email;
    public string Password { get; } = password;
    public string ClientId { get; } = clientId;
    public string? UserAgent { get; } = userAgent;
}
