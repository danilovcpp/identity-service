using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Features.Admin.Common;

public sealed class AdminWriteAuthorization(IAdminRepository administrators, IUserRepository users,
    IAdminAccessSettings settings, TimeProvider clock)
{
    // Caller owns an explicit transaction. All administrative writes serialize with CLI eligibility changes.
    public async Task<User?> LockAsync(Guid actorId, Guid actorSessionId, Guid? targetId, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) { throw new AdminUnavailableException(); }
        await administrators.LockAdministratorsAsync(cancellationToken);
        User? actor = null, target = null;
        foreach (var id in new[] { (Guid?)actorId, targetId }.OfType<Guid>().Distinct().Order())
        {
            var user = await users.FindByIdForUpdateAsync(id, cancellationToken);
            if (id == actorId) { actor = user; }
            if (id == targetId) { target = user; }
        }
        var session = await administrators.FindSessionForUpdateAsync(actorId, actorSessionId, cancellationToken);
        if (actor?.Status != UserStatus.Active || actor.EmailVerifiedAt is null || session is null || !session.IsActive(clock.GetUtcNow(), settings.IdleTimeout))
        { throw new UnauthorizedSessionException(); }
        if ((await administrators.FindRoleAsync(actorId, cancellationToken))?.Role != AdministratorRole.IdentityAdministrator)
        { throw new AdminForbiddenException(); }
        if (targetId is not null && target is null) { throw new AdminUserNotFoundException(); }
        return target;
    }
}
