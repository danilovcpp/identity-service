using Identity.Application.Abstractions.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Common;

public sealed class AdministratorGuard(IAdminRepository administrators)
{
    // Caller holds the global administrators lock, then the target user row lock.
    public async Task RequireRemovableAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Status == UserStatus.Active && user.EmailVerifiedAt is not null
            && (await administrators.FindRoleAsync(user.Id, cancellationToken))?.Role == AdministratorRole.IdentityAdministrator
            && await administrators.CountActiveAdministratorsAsync(cancellationToken) <= 1)
        {
            throw new LastAdministratorException();
        }
    }
}
