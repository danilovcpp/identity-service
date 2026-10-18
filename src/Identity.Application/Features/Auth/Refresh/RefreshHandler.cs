using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Auth.Refresh;

public sealed class RefreshHandler(ISessionRepository sessions, IUserRepository users, IUnitOfWork work,
    ILoginSettings settings, ITokenGenerator tokens, ITokenService accessTokens, TimeProvider clock, OperationContext operation)
    : ICommandHandler<RefreshCommand, TokenPairResult>
{
    public async Task<TokenPairResult> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) { throw new InvalidRefreshTokenException(); }
        var hash = tokens.Hash(command.RefreshToken);
        // Read only immutable identifiers before acquiring locks; never track token state here.
        var owner = await sessions.FindRefreshOwnerAsync(hash, cancellationToken);
        if (owner is null) { throw new InvalidRefreshTokenException(); }
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdForUpdateAsync(owner.UserId, cancellationToken);
        var session = await sessions.FindOwnedForUpdateAsync(owner.UserId, owner.SessionId, cancellationToken);
        // Fetch fresh state AFTER the user/session locks, so a queued duplicate sees consumption.
        var token = await sessions.FindRefreshAsync(hash, cancellationToken);
        var now = clock.GetUtcNow();
        if (session is null || token is null || token.SessionId != session.Id) { throw new InvalidRefreshTokenException(); }
        if (token.ConsumedAt is not null)
        {
            session.Revoke(SessionRevokeReason.RefreshReplay, now);
            await sessions.RevokeTokensAsync(session.Id, now, cancellationToken);
            sessions.Add(SecurityAuditEvent.Create(SecurityEventType.RefreshReplayDetected, session.UserId, session.ClientId,
                operation.CorrelationId, new AuditMetadata(), now));
            await work.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            // The revocation must commit even though the HTTP operation is rejected.
            throw new InvalidRefreshTokenException();
        }
        if (user?.Status != UserStatus.Active || user.LockedUntil > now || !session.IsActive(now) || !token.IsUsable(now)
            || session.Audience is null || settings.FindAudience(session.ClientId) != session.Audience)
        {
            throw new InvalidRefreshTokenException();
        }
        var raw = tokens.Generate();
        var expiry = now + settings.RefreshLifetime;
        if (expiry > session.AbsoluteExpiresAt) { expiry = session.AbsoluteExpiresAt; }
        var replacement = RefreshToken.Create(session, tokens.Hash(raw), now, expiry);
        token.Consume(replacement, now);
        session.Touch(now);
        sessions.Add(replacement);
        sessions.Add(SecurityAuditEvent.Create(SecurityEventType.RefreshRotated, user.Id, session.ClientId,
            operation.CorrelationId, new AuditMetadata(), now));
        var access = await accessTokens.IssueAsync(new AccessTokenSubject(user.Id, session.Id, session.ClientId, session.Audience), cancellationToken);
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TokenPairResult(access.Token, raw, access.LifetimeSeconds);
    }
}
