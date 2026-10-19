using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Users.EnableUser;

public sealed class EnableUserHandler(IAdminRepository administrators, IUnitOfWork work, AdminWriteAuthorization authorization,
    TimeProvider clock, OperationContext operation) : ICommandHandler<EnableUserCommand, Unit>
{
    public async Task<Unit> Handle(EnableUserCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = (await authorization.LockAsync(command.ActorId, command.ActorSessionId, command.UserId, cancellationToken))!;
        if (user.Status == UserStatus.Deactivated) { throw new AdminUserConflictException(); }
        if (user.Status == UserStatus.Disabled)
        {
            var now = clock.GetUtcNow();
            user.Enable(now);
            administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminEnabledUser, user.Id, null, operation.CorrelationId,
                new AuditMetadata(), now, command.ActorId, user.Id));
            await work.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
