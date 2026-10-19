using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Users.RequestPasswordReset;

public sealed class RequestAdminPasswordResetHandler(IAdminRepository administrators, ICredentialRepository credentials,
    IPasswordResetRepository resets, IUnitOfWork work, AdminWriteAuthorization authorization, IPasswordResetSettings settings,
    ITokenGenerator tokens, IPasswordResetEmailFactory emails, TimeProvider clock, OperationContext operation)
    : ICommandHandler<RequestAdminPasswordResetCommand, Unit>
{
    public async Task<Unit> Handle(RequestAdminPasswordResetCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = (await authorization.LockAsync(command.ActorId, command.ActorSessionId, command.UserId, cancellationToken))!;
        if (!settings.Enabled) { throw new PasswordResetUnavailableException(); }
        if (user.Status != UserStatus.Active || user.EmailVerifiedAt is null || await credentials.FindByUserIdAsync(user.Id, cancellationToken) is null)
        { throw new AdminUserConflictException(); }
        var now = clock.GetUtcNow();
        await resets.InvalidateOutstandingAsync(user.Id, now, null, cancellationToken);
        var raw = tokens.Generate();
        resets.Add(OneTimeToken.Create(user.Id, OneTimeTokenPurpose.PasswordReset, tokens.Hash(raw), now, now.AddMinutes(30)));
        resets.Add(emails.Create(user.Email, raw, now));
        administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminRequestedPasswordReset, user.Id, null, operation.CorrelationId,
            new AuditMetadata(), now, command.ActorId, user.Id));
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
