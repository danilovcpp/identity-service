using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Users.ResendInvitation;

public sealed record ResendInvitationCommand(Guid ActorId, Guid ActorSessionId, Guid UserId) : ICommand<Unit>;
