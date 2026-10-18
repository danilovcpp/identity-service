using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Auth.Login;

public sealed class LoginHandler(IUserRepository users, ICredentialRepository credentials, ILoginRepository login,
    IUnitOfWork work, ILoginSettings settings, ILoginPasswordVerifier verifier, IPasswordService passwords,
    ITokenGenerator tokens, ITokenService accessTokens, TimeProvider clock, OperationContext operation) : ICommandHandler<LoginCommand, TokenPairResult>
{
    public async Task<TokenPairResult> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) { throw new LoginDisabledException(); }
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        // Serialize verification, counters, rehash and session creation for this account.
        var user = await users.FindByNormalizedEmailForUpdateAsync(EmailAddress.Normalize(command.Email), cancellationToken);
        var credential = user is null ? null : await credentials.FindByUserIdAsync(user.Id, cancellationToken);
        // Unknown, disabled and locked accounts still perform password verification.
        var verification = verifier.Verify(credential?.PasswordHash, command.Password);
        var audience = settings.FindAudience(command.ClientId);
        var now = clock.GetUtcNow();
        var eligible = user?.Status == UserStatus.Active && (user.LockedUntil is null || user.LockedUntil <= now);
        if (!eligible || credential is null || verification == PasswordVerificationResult.Failed || audience is null)
        {
            if (eligible && verification == PasswordVerificationResult.Failed)
            {
                if (user!.LockedUntil is not null) { user.ResetLoginFailures(now); }
                user!.RecordFailedLogin(now, user.FailedLoginCount >= settings.FailureLimit - 1 ? now + settings.LockoutDuration : null);
            }
            login.Add(SecurityAuditEvent.Create(SecurityEventType.LoginFailed, user?.Id, command.ClientId,
                operation.CorrelationId, new AuditMetadata(), now));
            await work.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new InvalidCredentialsException();
        }

        user!.ResetLoginFailures(now);
        if (verification == PasswordVerificationResult.SuccessRehashNeeded) { credential.Rehash(passwords.Hash(command.Password)); }
        var session = Session.Create(user.Id, command.ClientId, command.UserAgent, now, now + settings.SessionLifetime, audience);
        var rawRefresh = tokens.Generate();
        login.Add(session);
        login.Add(RefreshToken.Create(session, tokens.Hash(rawRefresh), now,
            now + (settings.RefreshLifetime < settings.SessionLifetime ? settings.RefreshLifetime : settings.SessionLifetime)));
        login.Add(SecurityAuditEvent.Create(SecurityEventType.LoginSuccess, user.Id, command.ClientId,
            operation.CorrelationId, new AuditMetadata(), now));
        var access = await accessTokens.IssueAsync(new AccessTokenSubject(user.Id, session.Id, command.ClientId, audience), cancellationToken);
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TokenPairResult(access.Token, rawRefresh, access.LifetimeSeconds);
    }
}
