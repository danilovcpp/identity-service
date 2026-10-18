using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Auth.LogoutAll;

public sealed record LogoutAllCommand(Guid UserId, Guid CurrentSessionId) : ICommand<Unit>;
