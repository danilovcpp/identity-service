using Identity.Application.Abstractions.Cqrs;
using Identity.Domain.Enums;

namespace Identity.Application.Features.Admin.Access.GrantAdministrator;

// Exactly one selector is required. CLI only; never exposed by a controller.
public sealed record GrantAdministratorCommand(Guid? UserId, string? Email, AdministratorRole Role) : ICommand<Unit>;
