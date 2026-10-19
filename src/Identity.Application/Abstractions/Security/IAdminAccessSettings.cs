namespace Identity.Application.Abstractions.Security;

public interface IAdminAccessSettings
{
    bool Enabled { get; }
    TimeSpan IdleTimeout { get; }
    TimeSpan AbsoluteLifetime { get; }
}
