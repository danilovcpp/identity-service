using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Users.InviteUser;

public sealed class InviteUserCommand(Guid actorId, Guid actorSessionId, string email, string? displayName) : ICommand<Unit>
{
    public Guid ActorId { get; } = actorId;
    public Guid ActorSessionId { get; } = actorSessionId;
    public string Email { get; } = email;
    public string? DisplayName { get; } = displayName;
}
