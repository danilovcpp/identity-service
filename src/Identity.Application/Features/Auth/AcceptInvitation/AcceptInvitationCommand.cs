using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Auth.AcceptInvitation;

public sealed class AcceptInvitationCommand(string token, string newPassword) : ICommand<Unit>
{
    public string Token { get; } = token;
    public string NewPassword { get; } = newPassword;
}
