namespace Identity.Application.Abstractions.Security;

public interface IInvitationSettings
{
    bool Enabled { get; }
    TimeSpan Lifetime { get; }
}
