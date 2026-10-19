using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Enums;

namespace Identity.Application.Features.Admin.Auth.ValidateSession;

public sealed class ValidateAdminSessionHandler(IAdminRepository administrators, IUserRepository users, IUnitOfWork work,
    IAdminAccessSettings settings, TimeProvider clock) : ICommandHandler<ValidateAdminSessionCommand, AdministratorRole?>
{
    public async Task<AdministratorRole?> Handle(ValidateAdminSessionCommand command, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) { return null; }
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdForUpdateAsync(command.UserId, cancellationToken);
        var session = await administrators.FindSessionForUpdateAsync(command.UserId, command.SessionId, cancellationToken);
        var role = await administrators.FindRoleAsync(command.UserId, cancellationToken);
        var now = clock.GetUtcNow();
        if (user?.Status != UserStatus.Active || user.EmailVerifiedAt is null || role is null || session is null
            || !session.IsActive(now, settings.IdleTimeout)) { return null; }
        session.Touch(now, settings.IdleTimeout);
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return role.Role;
    }
}
