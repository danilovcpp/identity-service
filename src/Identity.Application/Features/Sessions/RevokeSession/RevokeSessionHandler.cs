using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Sessions.RevokeSession;

public sealed class RevokeSessionHandler(ISessionRepository sessions, IUserRepository users, IUnitOfWork work,
    TimeProvider clock, OperationContext operation) : ICommandHandler<RevokeSessionCommand, Unit>
{
    public async Task<Unit> Handle(RevokeSessionCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdForUpdateAsync(command.UserId, cancellationToken);
        var current = await sessions.FindOwnedForUpdateAsync(command.UserId, command.CurrentSessionId, cancellationToken);
        var now = clock.GetUtcNow();
        SessionAuthorization.RequireActive(user, current, now);
        var session = await sessions.FindOwnedForUpdateAsync(command.UserId, command.SessionId, cancellationToken)
            ?? throw new SessionNotFoundException();
        if (session.RevokedAt is null)
        {
            session.Revoke(SessionRevokeReason.UserRevoked, now);
            await sessions.RevokeTokensAsync(session.Id, now, cancellationToken);
            sessions.Add(SecurityAuditEvent.Create(SecurityEventType.Logout, command.UserId, session.ClientId,
                operation.CorrelationId, new AuditMetadata(), now));
            await work.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
