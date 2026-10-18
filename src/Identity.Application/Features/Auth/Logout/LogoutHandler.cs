using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Auth.Logout;

public sealed class LogoutHandler(ISessionRepository sessions, IUserRepository users, IUnitOfWork work,
    ITokenGenerator tokens, TimeProvider clock, OperationContext operation) : ICommandHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (!OpaqueTokenText.IsSupported(command.RefreshToken)) { return new Unit(); }
        var owner = await sessions.FindRefreshOwnerAsync(tokens.Hash(command.RefreshToken), cancellationToken);
        if (owner is null) { return new Unit(); }
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        await users.FindByIdForUpdateAsync(owner.UserId, cancellationToken);
        var session = await sessions.FindOwnedForUpdateAsync(owner.UserId, owner.SessionId, cancellationToken);
        if (session is not null && session.RevokedAt is null)
        {
            var now = clock.GetUtcNow();
            session.Revoke(SessionRevokeReason.Logout, now);
            await sessions.RevokeTokensAsync(session.Id, now, cancellationToken);
            sessions.Add(SecurityAuditEvent.Create(SecurityEventType.Logout, session.UserId, session.ClientId,
                operation.CorrelationId, new AuditMetadata(), now));
            await work.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
