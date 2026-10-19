using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Auth.Logout;

public sealed class AdminLogoutHandler(IAdminRepository administrators, IUserRepository users, IUnitOfWork work,
    TimeProvider clock, OperationContext operation) : ICommandHandler<AdminLogoutCommand, Unit>
{
    public async Task<Unit> Handle(AdminLogoutCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        await users.FindByIdForUpdateAsync(command.UserId, cancellationToken);
        var session = await administrators.FindSessionForUpdateAsync(command.UserId, command.SessionId, cancellationToken);
        if (session is not null && session.RevokedAt is null)
        {
            var now = clock.GetUtcNow();
            session.Revoke(now);
            administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminLogout, command.UserId, null,
                operation.CorrelationId, new AuditMetadata(), now, command.UserId, command.UserId));
            await work.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
