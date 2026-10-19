using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Access.RevokeAdministrator;

public sealed record RevokeAdministratorCommand(Guid? UserId, string? Email) : ICommand<Unit>;
