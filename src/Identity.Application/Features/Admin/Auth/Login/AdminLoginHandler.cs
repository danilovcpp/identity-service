using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Admin.Auth.Login;

public sealed class AdminLoginHandler(IUserRepository users, ICredentialRepository credentials, IAdminRepository administrators,
    IUnitOfWork work, IAdminAccessSettings settings, ILoginSettings loginSettings, ILoginPasswordVerifier verifier,
    IPasswordService passwords, TimeProvider clock, OperationContext operation) : ICommandHandler<AdminLoginCommand, AdminLoginResult>
{
    public async Task<AdminLoginResult> Handle(AdminLoginCommand command, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) { throw new AdminUnavailableException(); }
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByNormalizedEmailForUpdateAsync(EmailAddress.Normalize(command.Email), cancellationToken);
        var credential = user is null ? null : await credentials.FindByUserIdAsync(user.Id, cancellationToken);
        var verification = verifier.Verify(credential?.PasswordHash, command.Password);
        var now = clock.GetUtcNow();
        var eligible = user?.Status == UserStatus.Active && user.EmailVerifiedAt is not null
            && (user.LockedUntil is null || user.LockedUntil <= now);
        var role = user is null ? null : await administrators.FindRoleAsync(user.Id, cancellationToken);
        if (!eligible || credential is null || verification == PasswordVerificationResult.Failed || role is null)
        {
            if (eligible && verification == PasswordVerificationResult.Failed)
            {
                if (user!.LockedUntil is not null) { user.ResetLoginFailures(now); }
                user!.RecordFailedLogin(now, user.FailedLoginCount >= loginSettings.FailureLimit - 1 ? now + loginSettings.LockoutDuration : null);
            }
            administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminLoginFailed, user?.Id, null,
                operation.CorrelationId, new AuditMetadata(), now, targetUserId: user?.Id));
            await work.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (eligible && credential is not null && verification != PasswordVerificationResult.Failed && role is null)
            {
                throw new AdminForbiddenException();
            }
            throw new InvalidCredentialsException();
        }

        user!.ResetLoginFailures(now);
        if (verification == PasswordVerificationResult.SuccessRehashNeeded) { credential.Rehash(passwords.Hash(command.Password)); }
        var session = AdminSession.Create(user.Id, now, now + settings.AbsoluteLifetime);
        administrators.Add(session);
        administrators.Add(SecurityAuditEvent.Create(SecurityEventType.AdminLogin, user.Id, null, operation.CorrelationId,
            new AuditMetadata(), now, user.Id, user.Id));
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AdminLoginResult(session.Id, session.CreatedAt, session.ExpiresAt,
            new AdminProfile(user.Id, user.Email, user.DisplayName, role.Role.ToString()));
    }
}
