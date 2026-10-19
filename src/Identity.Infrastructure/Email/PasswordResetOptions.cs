using Identity.Application.Abstractions.Security;

namespace Identity.Infrastructure.Email;

public sealed class PasswordResetOptions : IPasswordResetSettings
{
    public bool Enabled { get; set; }
}
