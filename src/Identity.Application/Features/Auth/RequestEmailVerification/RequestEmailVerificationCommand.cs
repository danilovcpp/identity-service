using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Auth.RequestEmailVerification;

public sealed class RequestEmailVerificationCommand(string email) : ICommand<Unit>
{
    public string Email { get; } = email;
}
