using Identity.Application.Abstractions.Persistence;
using Identity.Domain.Enums;

namespace Identity.Application.Common;

public sealed class SessionRevoker(ISessionRepository sessions)
{
    // The caller owns the transaction and user row lock; all use cases share the same lock order.
    public async Task RevokeAllAsync(Guid userId, SessionRevokeReason reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var session in await sessions.FindAllForUpdateAsync(userId, cancellationToken))
        {
            session.Revoke(reason, now);
            await sessions.RevokeTokensAsync(session.Id, now, cancellationToken);
        }
    }
}
