using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Persistence;

public interface IAdminRepository
{
    // Transaction-wide lock. Take before any user lock when changing administrative eligibility.
    Task LockAdministratorsAsync(CancellationToken cancellationToken);
    Task<IdentityAdministrator?> FindRoleAsync(Guid userId, CancellationToken cancellationToken);
    Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken);
    void Add(IdentityAdministrator administrator);
    void Remove(IdentityAdministrator administrator);
    void Add(AdminSession session);
    void Add(SecurityAuditEvent auditEvent);
    Task<AdminSession?> FindSessionForUpdateAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task RevokeAllSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<AdminProfile?> ReadProfileAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record AdminProfile(Guid Id, string Email, string? DisplayName, string Role);
