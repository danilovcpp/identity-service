using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Users.DeactivateAccount;

public sealed class DeactivateAccountHandler(IUserRepository users, ISessionRepository sessions, ICredentialRepository credentials,
    IPasswordResetRepository resets, IPasswordService passwords, IUnitOfWork work, SessionRevoker revoker,
    TimeProvider clock, OperationContext operation) : ICommandHandler<DeactivateAccountCommand, Unit>
{
    public async Task<Unit> Handle(DeactivateAccountCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdForUpdateAsync(command.UserId, cancellationToken);
        var session = await sessions.FindOwnedForUpdateAsync(command.UserId, command.CurrentSessionId, cancellationToken);
        var now = clock.GetUtcNow();
        SessionAuthorization.RequireActive(user, session, now);
        var credential = await credentials.FindByUserIdAsync(command.UserId, cancellationToken);
        if (credential is null || passwords.Verify(credential.PasswordHash, command.Password) == PasswordVerificationResult.Failed)
        {
            throw new InvalidCredentialsException();
        }
        user!.Deactivate(now);
        await revoker.RevokeAllAsync(user.Id, SessionRevokeReason.AccountDeactivated, now, cancellationToken);
        await resets.InvalidateOutstandingAsync(user.Id, now, null, cancellationToken);
        sessions.Add(SecurityAuditEvent.Create(SecurityEventType.AccountDeactivated, user.Id, session!.ClientId,
            operation.CorrelationId, new AuditMetadata(), now));
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
