using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Auth.ResetPassword;

public sealed class ResetPasswordCommand(string token, string newPassword) : ICommand<Unit>
{
    public string Token { get; } = token;
    public string NewPassword { get; } = newPassword;
}
