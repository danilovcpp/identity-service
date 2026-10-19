using Identity.Domain.Common;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class IdentityAdministrator
{
    private IdentityAdministrator() { }
    public Guid UserId { get; private set; }
    public AdministratorRole Role { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public Guid? GrantedBy { get; private set; }

    public static IdentityAdministrator Grant(User user, AdministratorRole role, Guid? grantedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);
        DomainRules.Require(user.Status == UserStatus.Active && user.EmailVerifiedAt is not null,
            "admin_user_ineligible", "Only an active, verified user can be an administrator.");
        DomainRules.Require(Enum.IsDefined(role), "invalid_admin_role", "The administrator role is invalid.");
        return new IdentityAdministrator
        {
            UserId = user.Id,
            Role = role,
            GrantedAt = DomainRules.Time(now, user.CreatedAt),
            GrantedBy = grantedBy is null ? null : DomainRules.Identifier(grantedBy.Value)
        };
    }
}
