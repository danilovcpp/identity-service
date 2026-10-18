using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Auth.Register;

public sealed class RegisterHandler(IUserRepository users, ICredentialRepository credentials,
    IEmailVerificationRepository verification, IUnitOfWork work, IPasswordService passwords,
    ITokenGenerator tokens, IVerificationEmailFactory emails, IRegistrationSettings settings,
    TimeProvider clock, OperationContext operation) : ICommandHandler<RegisterCommand, Unit>
{
    public async Task<Unit> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) { throw new RegistrationDisabledException(); }
        // Do the same expensive hashing/encryption work for a duplicate; no early existence lookup.
        var passwordHash = passwords.Hash(command.Password);
        var rawToken = tokens.Generate();
        var now = clock.GetUtcNow();
        var user = User.Create(command.Email, command.DisplayName, now);
        var message = emails.Create(user.Email, rawToken, now);
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        users.Add(user);
        credentials.Add(Credential.Create(user.Id, passwordHash, now));
        verification.Add(OneTimeToken.Create(user.Id, OneTimeTokenPurpose.EmailVerification, tokens.Hash(rawToken), now, now.AddHours(24)));
        verification.Add(message);
        verification.Add(SecurityAuditEvent.Create(SecurityEventType.Register, user.Id, command.ClientId,
            operation.CorrelationId, new AuditMetadata(), now));
        try
        {
            await work.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DuplicateEmailException)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        return new Unit();
    }
}
