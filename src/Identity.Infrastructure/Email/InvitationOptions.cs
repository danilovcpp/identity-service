using Identity.Application.Abstractions.Security;

namespace Identity.Infrastructure.Email;

public sealed class InvitationOptions : IInvitationSettings
{
    public bool Enabled { get; set; }
    public int LifetimeSeconds { get; set; } = 259200;
    public TimeSpan Lifetime => TimeSpan.FromSeconds(LifetimeSeconds);
}
