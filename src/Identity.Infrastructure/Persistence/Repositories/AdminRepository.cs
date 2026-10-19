using Identity.Application.Abstractions.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

internal sealed class AdminRepository(IdentityDbContext context) : IAdminRepository
{
    public async Task LockAdministratorsAsync(CancellationToken cancellationToken)
    {
        RequireTransaction();
        // Stable, product-specific lock shared by CLI and every loss-of-eligibility use case.
        await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731849205194218)", cancellationToken);
    }

    public Task<IdentityAdministrator?> FindRoleAsync(Guid userId, CancellationToken cancellationToken) =>
        context.IdentityAdministrators.SingleOrDefaultAsync(role => role.UserId == userId, cancellationToken);

    public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken) =>
        (from role in context.IdentityAdministrators.AsNoTracking()
         join user in context.Users.AsNoTracking() on role.UserId equals user.Id
         where role.Role == AdministratorRole.IdentityAdministrator && user.Status == UserStatus.Active && user.EmailVerifiedAt != null
         select role.UserId).CountAsync(cancellationToken);

    public void Add(IdentityAdministrator administrator) => context.IdentityAdministrators.Add(administrator);
    public void Remove(IdentityAdministrator administrator) => context.IdentityAdministrators.Remove(administrator);
    public void Add(AdminSession session) => context.AdminSessions.Add(session);
    public void Add(SecurityAuditEvent auditEvent) => context.SecurityAuditEvents.Add(auditEvent);

    public Task<AdminSession?> FindSessionForUpdateAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        RequireTransaction();
        return context.AdminSessions.FromSqlInterpolated($"SELECT *, xmin FROM admin_sessions WHERE user_id = {userId} AND id = {sessionId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task RevokeAllSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        RequireTransaction();
        return context.Database.ExecuteSqlInterpolatedAsync($"UPDATE admin_sessions SET revoked_at = GREATEST({now}, created_at) WHERE user_id = {userId} AND revoked_at IS NULL", cancellationToken);
    }

    public Task<AdminProfile?> ReadProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        (from role in context.IdentityAdministrators.AsNoTracking()
         join user in context.Users.AsNoTracking() on role.UserId equals user.Id
         where user.Id == userId && user.Status == UserStatus.Active && user.EmailVerifiedAt != null
         select new AdminProfile(user.Id, user.Email, user.DisplayName, role.Role.ToString())).SingleOrDefaultAsync(cancellationToken);

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null) { throw new InvalidOperationException("Administrator locks require an explicit transaction."); }
    }
}
