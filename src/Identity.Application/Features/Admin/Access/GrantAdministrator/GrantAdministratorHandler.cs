using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Access.GrantAdministrator;

public sealed class GrantAdministratorHandler(IAdminRepository administrators, IUserRepository users, IUnitOfWork work,
    AdministratorGuard guard, TimeProvider clock, OperationContext operation) : ICommandHandler<GrantAdministratorCommand, Unit>
{
    public async Task<Unit> Handle(GrantAdministratorCommand command, CancellationToken cancellationToken)
    {
        if ((command.UserId is null) == (command.Email is null) || command.UserId == Guid.Empty || !Enum.IsDefined(command.Role))
        { throw new AdminTargetIneligibleException(); }
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        await administrators.LockAdministratorsAsync(cancellationToken);
        var user = command.UserId is { } id ? await users.FindByIdForUpdateAsync(id, cancellationToken)
            : await users.FindByNormalizedEmailForUpdateAsync(EmailAddress.Normalize(command.Email!), cancellationToken);
        if (user?.Status != UserStatus.Active || user.EmailVerifiedAt is null) { throw new AdminTargetIneligibleException(); }
        var existing = await administrators.FindRoleAsync(user.Id, cancellationToken);
        if (existing?.Role == command.Role) { await transaction.CommitAsync(cancellationToken); return new Unit(); }
        if (existing is not null)
        {
            await guard.RequireRemovableAsync(user, cancellationToken);
            administrators.Remove(existing);
            // Delete before re-inserting the same primary key, within this transaction.
            await work.SaveChangesAsync(cancellationToken);
        }
        var now = clock.GetUtcNow();
        await administrators.RevokeAllSessionsAsync(user.Id, now, cancellationToken);
        administrators.Add(IdentityAdministrator.Grant(user, command.Role, null, now));
        administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminRoleGranted, user.Id, null,
            operation.CorrelationId, new AuditMetadata(), now, targetUserId: user.Id));
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
