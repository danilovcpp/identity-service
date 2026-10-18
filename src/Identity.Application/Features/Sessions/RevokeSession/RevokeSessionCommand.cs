using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Sessions.RevokeSession;

public sealed record RevokeSessionCommand(Guid UserId, Guid CurrentSessionId, Guid SessionId) : ICommand<Unit>;
