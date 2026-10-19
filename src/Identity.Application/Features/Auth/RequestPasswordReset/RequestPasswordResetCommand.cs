using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Auth.RequestPasswordReset;

public sealed class RequestPasswordResetCommand(string email) : ICommand<Unit>
{
    public string Email { get; } = email;
}
