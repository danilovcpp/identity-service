using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Users.DeactivateAccount;

public sealed class DeactivateAccountCommand(Guid userId, Guid currentSessionId, string password) : ICommand<Unit>
{
    public Guid UserId { get; } = userId;
    public Guid CurrentSessionId { get; } = currentSessionId;
    public string Password { get; } = password;
}
