namespace Identity.Application.Abstractions.Security;

public interface ILoginSettings
{
    bool Enabled { get; }
    string? FindAudience(string clientId);
    int FailureLimit { get; }
    TimeSpan LockoutDuration { get; }
    TimeSpan RefreshLifetime { get; }
    TimeSpan SessionLifetime { get; }
}

public interface ILoginPasswordVerifier
{
    PasswordVerificationResult Verify(string? passwordHash, string password);
}
