using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Auth.Logout;

public sealed record AdminLogoutCommand(Guid UserId, Guid SessionId) : ICommand<Unit>;
