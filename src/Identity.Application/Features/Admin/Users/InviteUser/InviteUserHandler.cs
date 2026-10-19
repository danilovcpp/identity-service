using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Users.InviteUser;

public sealed class InviteUserHandler(IUserRepository users, IAdminRepository administrators, IUnitOfWork work,
    AdminWriteAuthorization authorization, InvitationIssuer issuer, IInvitationSettings settings, TimeProvider clock, OperationContext operation)
    : ICommandHandler<InviteUserCommand, Unit>
{
    public async Task<Unit> Handle(InviteUserCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        await authorization.LockAsync(command.ActorId, command.ActorSessionId, null, cancellationToken);
        if (!settings.Enabled) { throw new InvitationUnavailableException(); }
        if (await users.FindByNormalizedEmailAsync(EmailAddress.Normalize(command.Email), cancellationToken) is not null)
        { throw new AdminEmailConflictException(); }
        var now = clock.GetUtcNow();
        var user = User.CreateInvited(command.Email, command.DisplayName, now);
        users.Add(user);
        await issuer.IssueAsync(user, now, cancellationToken);
        administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminInvitedUser, user.Id, null, operation.CorrelationId,
            new AuditMetadata(), now, command.ActorId, user.Id));
        try
        {
            await work.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DuplicateEmailException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new AdminEmailConflictException();
        }
        return new Unit();
    }
}
