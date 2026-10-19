using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Users.DisableUser;

public sealed class DisableUserHandler(IAdminRepository administrators, IInvitationRepository invitations, IUnitOfWork work,
    AdminWriteAuthorization authorization, AdministratorGuard guard, SessionRevoker revoker, TimeProvider clock, OperationContext operation)
    : ICommandHandler<DisableUserCommand, Unit>
{
    public async Task<Unit> Handle(DisableUserCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = (await authorization.LockAsync(command.ActorId, command.ActorSessionId, command.UserId, cancellationToken))!;
        if (user.Status == UserStatus.Deactivated) { throw new AdminUserConflictException(); }
        if (user.Status != UserStatus.Disabled)
        {
            await guard.RequireRemovableAsync(user, cancellationToken);
            var now = clock.GetUtcNow();
            user.Disable(now, command.ActorId, command.Reason);
            await revoker.RevokeAllAsync(user.Id, SessionRevokeReason.AccountDisabled, now, cancellationToken);
            await invitations.InvalidateAllPurposesAsync(user.Id, now, cancellationToken);
            administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminDisabledUser, user.Id, null, operation.CorrelationId,
                new AuditMetadata(), now, command.ActorId, user.Id));
            await work.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
