using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Users.ResendInvitation;

public sealed class ResendInvitationHandler(IAdminRepository administrators, ICredentialRepository credentials, IUnitOfWork work,
    AdminWriteAuthorization authorization, InvitationIssuer issuer, IInvitationSettings settings, TimeProvider clock, OperationContext operation)
    : ICommandHandler<ResendInvitationCommand, Unit>
{
    public async Task<Unit> Handle(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = (await authorization.LockAsync(command.ActorId, command.ActorSessionId, command.UserId, cancellationToken))!;
        if (!settings.Enabled) { throw new InvitationUnavailableException(); }
        if (user.Status != UserStatus.Invited || await credentials.FindByUserIdAsync(user.Id, cancellationToken) is not null)
        { throw new AdminUserConflictException(); }
        var now = clock.GetUtcNow();
        await issuer.IssueAsync(user, now, cancellationToken);
        administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminResentInvitation, user.Id, null, operation.CorrelationId,
            new AuditMetadata(), now, command.ActorId, user.Id));
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
