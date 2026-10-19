using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Auth.ResetPassword;

public sealed class ResetPasswordHandler(IUserRepository users, ICredentialRepository credentials, IPasswordResetRepository resets,
    ISessionRepository sessions, IUnitOfWork work, ITokenGenerator tokens, IPasswordService passwords, IPasswordResetSettings settings,
    SessionRevoker revoker, TimeProvider clock, OperationContext operation) : ICommandHandler<ResetPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) { throw new InvalidPasswordResetTokenException(); }
        var hash = tokens.Hash(command.Token);
        var owner = await resets.FindOwnerAsync(hash, cancellationToken);
        if (owner is null) { throw new InvalidPasswordResetTokenException(); }
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdForUpdateAsync(owner.Value, cancellationToken);
        var token = await resets.FindForUpdateAsync(hash, cancellationToken);
        var now = clock.GetUtcNow();
        if (user?.Status != UserStatus.Active || token is null || token.UserId != user.Id || !token.IsUsable(now))
        {
            throw new InvalidPasswordResetTokenException();
        }
        var credential = await credentials.FindByUserIdAsync(user.Id, cancellationToken) ?? throw new InvalidPasswordResetTokenException();
        token.Consume(now);
        credential.ChangePasswordHash(passwords.Hash(command.NewPassword), now);
        user.ResetLoginFailures(now);
        await revoker.RevokeAllAsync(user.Id, SessionRevokeReason.PasswordReset, now, cancellationToken);
        // Exclude the tracked token: bulk updating it would invalidate its xmin before SaveChanges.
        await resets.InvalidateOutstandingAsync(user.Id, now, token.Id, cancellationToken);
        sessions.Add(SecurityAuditEvent.Create(SecurityEventType.PasswordReset, user.Id, null, operation.CorrelationId, new AuditMetadata(), now));
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
