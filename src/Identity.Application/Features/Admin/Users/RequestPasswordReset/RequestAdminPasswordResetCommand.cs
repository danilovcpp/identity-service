using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Users.RequestPasswordReset;

public sealed record RequestAdminPasswordResetCommand(Guid ActorId, Guid ActorSessionId, Guid UserId) : ICommand<Unit>;
