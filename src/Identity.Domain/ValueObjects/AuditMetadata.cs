using Identity.Domain.Common;

namespace Identity.Domain.ValueObjects;

// An explicit whitelist: no arbitrary strings, IP addresses, tokens or email addresses.
public sealed class AuditMetadata
{
    public AuditMetadata(int? attemptCount = null)
    {
        DomainRules.Require(attemptCount is null or >= 0, "invalid_audit_metadata", "The attempt count cannot be negative.");
        AttemptCount = attemptCount;
    }

    public int? AttemptCount { get; }
}
