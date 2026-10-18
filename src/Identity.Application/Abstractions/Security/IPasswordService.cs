namespace Identity.Application.Abstractions.Security;

public interface IPasswordService
{
    string Hash(string password);
    PasswordVerificationResult Verify(string passwordHash, string password);
}

public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded
}
