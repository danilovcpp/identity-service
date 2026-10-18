using Identity.Application.Abstractions.Security;

namespace Identity.Infrastructure.Email;

public sealed class RegistrationOptions : IRegistrationSettings
{
    public bool Enabled { get; set; }
}
