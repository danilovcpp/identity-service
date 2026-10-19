using Identity.Application.Abstractions.Cqrs;
using Identity.Domain.Enums;

namespace Identity.Application.Features.Admin.Auth.ValidateSession;

public sealed record ValidateAdminSessionCommand(Guid UserId, Guid SessionId) : ICommand<AdministratorRole?>;
