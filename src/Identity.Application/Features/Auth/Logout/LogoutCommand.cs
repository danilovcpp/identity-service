using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Auth.Logout;

public sealed class LogoutCommand(string refreshToken) : ICommand<Unit>
{
    public string RefreshToken { get; } = refreshToken;
}
