using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Users.RevokeUserSession;

public sealed class RevokeUserSessionHandler(IAdminRepository administrators, ISessionRepository sessions, IUnitOfWork work,
    AdminWriteAuthorization authorization, TimeProvider clock, OperationContext operation) : ICommandHandler<RevokeUserSessionCommand, Unit>
{
    public async Task<Unit> Handle(RevokeUserSessionCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        await authorization.LockAsync(command.ActorId, command.ActorSessionId, command.UserId, cancellationToken);
        var session = await sessions.FindOwnedForUpdateAsync(command.UserId, command.SessionId, cancellationToken) ?? throw new SessionNotFoundException();
        if (session.RevokedAt is null)
        {
            var now = clock.GetUtcNow();
            session.Revoke(SessionRevokeReason.AdministratorRevoked, now);
            await sessions.RevokeTokensAsync(session.Id, now, cancellationToken);
            administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminRevokedSession, command.UserId, session.ClientId,
                operation.CorrelationId, new AuditMetadata(), now, command.ActorId, command.UserId));
            await work.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
