using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Access.RevokeAdministrator;

public sealed class RevokeAdministratorHandler(IAdminRepository administrators, IUserRepository users, IUnitOfWork work,
    AdministratorGuard guard, TimeProvider clock, OperationContext operation) : ICommandHandler<RevokeAdministratorCommand, Unit>
{
    public async Task<Unit> Handle(RevokeAdministratorCommand command, CancellationToken cancellationToken)
    {
        if ((command.UserId is null) == (command.Email is null) || command.UserId == Guid.Empty) { throw new AdminTargetIneligibleException(); }
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        await administrators.LockAdministratorsAsync(cancellationToken);
        var user = command.UserId is { } id ? await users.FindByIdForUpdateAsync(id, cancellationToken)
            : await users.FindByNormalizedEmailForUpdateAsync(EmailAddress.Normalize(command.Email!), cancellationToken);
        if (user is null) { throw new AdminTargetIneligibleException(); }
        var existing = await administrators.FindRoleAsync(user.Id, cancellationToken);
        if (existing is not null)
        {
            await guard.RequireRemovableAsync(user, cancellationToken);
            administrators.Remove(existing);
            var now = clock.GetUtcNow();
            await administrators.RevokeAllSessionsAsync(user.Id, now, cancellationToken);
            administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminRoleRevoked, user.Id, null,
                operation.CorrelationId, new AuditMetadata(), now, targetUserId: user.Id));
            await work.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
