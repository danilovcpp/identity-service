using Identity.Application.Abstractions.Security;

namespace Identity.Infrastructure.Security;

public sealed class AdminAccessOptions : IAdminAccessSettings
{
    public bool Enabled { get; set; }
    public string DataProtectionKeyPath { get; set; } = "";
    public int IdleTimeoutSeconds { get; set; } = 1800;
    public int AbsoluteLifetimeSeconds { get; set; } = 28800;
    public TimeSpan IdleTimeout => TimeSpan.FromSeconds(IdleTimeoutSeconds);
    public TimeSpan AbsoluteLifetime => TimeSpan.FromSeconds(AbsoluteLifetimeSeconds);

    public static bool IsValid(AdminAccessOptions options) => options.IdleTimeoutSeconds is >= 60 and <= 1800
        && options.AbsoluteLifetimeSeconds is >= 60 and <= 28800 && options.IdleTimeoutSeconds <= options.AbsoluteLifetimeSeconds
        && (!options.Enabled || (Path.IsPathFullyQualified(options.DataProtectionKeyPath) && Directory.Exists(options.DataProtectionKeyPath)));
}
