using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Users.RevokeUserSession;

public sealed record RevokeUserSessionCommand(Guid ActorId, Guid ActorSessionId, Guid UserId, Guid SessionId) : ICommand<Unit>;
