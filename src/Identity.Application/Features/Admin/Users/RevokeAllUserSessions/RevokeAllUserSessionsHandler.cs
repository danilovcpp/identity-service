using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Users.RevokeAllUserSessions;

public sealed class RevokeAllUserSessionsHandler(IAdminRepository administrators, IUnitOfWork work, AdminWriteAuthorization authorization,
    SessionRevoker revoker, TimeProvider clock, OperationContext operation) : ICommandHandler<RevokeAllUserSessionsCommand, Unit>
{
    public async Task<Unit> Handle(RevokeAllUserSessionsCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        await authorization.LockAsync(command.ActorId, command.ActorSessionId, command.UserId, cancellationToken);
        var now = clock.GetUtcNow();
        await revoker.RevokeAllAsync(command.UserId, SessionRevokeReason.AdministratorRevoked, now, cancellationToken);
        administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminRevokedAllSessions, command.UserId, null, operation.CorrelationId,
            new AuditMetadata(), now, command.ActorId, command.UserId));
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
