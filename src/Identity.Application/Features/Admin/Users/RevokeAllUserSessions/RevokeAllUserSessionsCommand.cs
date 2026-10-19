using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Users.RevokeAllUserSessions;

public sealed record RevokeAllUserSessionsCommand(Guid ActorId, Guid ActorSessionId, Guid UserId) : ICommand<Unit>;
