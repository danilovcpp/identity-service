using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Auth.LogoutAll;

public sealed class LogoutAllHandler(ISessionRepository sessions, IUserRepository users, IUnitOfWork work,
    TimeProvider clock, OperationContext operation, SessionRevoker revoker) : ICommandHandler<LogoutAllCommand, Unit>
{
    public async Task<Unit> Handle(LogoutAllCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdForUpdateAsync(command.UserId, cancellationToken);
        var current = await sessions.FindOwnedForUpdateAsync(command.UserId, command.CurrentSessionId, cancellationToken);
        var now = clock.GetUtcNow();
        SessionAuthorization.RequireActive(user, current, now);
        await revoker.RevokeAllAsync(command.UserId, SessionRevokeReason.LogoutAll, now, cancellationToken);
        sessions.Add(SecurityAuditEvent.Create(SecurityEventType.LogoutAll, command.UserId, current!.ClientId,
            operation.CorrelationId, new AuditMetadata(), now));
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
