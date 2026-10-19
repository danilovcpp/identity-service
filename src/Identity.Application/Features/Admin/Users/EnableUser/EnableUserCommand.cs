using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Users.EnableUser;

public sealed record EnableUserCommand(Guid ActorId, Guid ActorSessionId, Guid UserId) : ICommand<Unit>;
