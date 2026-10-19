using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Users.DisableUser;

public sealed class DisableUserCommand(Guid actorId, Guid actorSessionId, Guid userId, string reason) : ICommand<Unit>
{
    public Guid ActorId { get; } = actorId;
    public Guid ActorSessionId { get; } = actorSessionId;
    public Guid UserId { get; } = userId;
    public string Reason { get; } = reason;
}
