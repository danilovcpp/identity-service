using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VerificationResult = Identity.Application.Abstractions.Security.PasswordVerificationResult;

namespace Identity.Infrastructure.Security;

public sealed class PasswordService : IPasswordService
{
    private static readonly object Subject = new();
    private readonly PasswordHasher<object> _hasher;

    public PasswordService(IOptions<PasswordHashOptions> options)
    {
        _hasher = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = options.Value.IterationCount
        }));
    }

    public string Hash(string password) => _hasher.HashPassword(Subject, PasswordText.Normalize(password));

    public VerificationResult Verify(string passwordHash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(Subject, passwordHash, PasswordText.Normalize(password)) switch
            {
                Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success => VerificationResult.Success,
                Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded => VerificationResult.SuccessRehashNeeded,
                _ => VerificationResult.Failed
            };
        }
        catch (ArgumentException) { return VerificationResult.Failed; }
        catch (FormatException) { return VerificationResult.Failed; }
    }
}
