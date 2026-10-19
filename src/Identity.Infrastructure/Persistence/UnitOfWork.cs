using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Identity.Infrastructure.Persistence;

internal sealed class UnitOfWork(IdentityDbContext context, AuthMetrics metrics) : IUnitOfWork
{
    private readonly List<SecurityAuditEvent> _pending = [];
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var events = context.ChangeTracker.Entries<SecurityAuditEvent>().Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity).ToArray();
            var count = await context.SaveChangesAsync(cancellationToken);
            if (context.Database.CurrentTransaction is null) { metrics.Committed(events); }
            else { _pending.AddRange(events); }
            return count;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_users_normalized_email" })
        {
            throw new DuplicateEmailException();
        }
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new UnitOfWorkTransaction(await context.Database.BeginTransactionAsync(cancellationToken), _pending, metrics);

    private sealed class UnitOfWorkTransaction(IDbContextTransaction transaction, List<SecurityAuditEvent> pending, AuthMetrics metrics) : IUnitOfWorkTransaction
    {
        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await transaction.CommitAsync(cancellationToken);
            metrics.Committed(pending);
            pending.Clear();
        }
        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            await transaction.RollbackAsync(cancellationToken);
            pending.Clear();
        }
        public async ValueTask DisposeAsync()
        {
            pending.Clear();
            await transaction.DisposeAsync();
        }
    }
}
