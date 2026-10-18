using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Auth.VerifyEmail;

public sealed class VerifyEmailCommand(string token) : ICommand<Unit>
{
    public string Token { get; } = token;
}
