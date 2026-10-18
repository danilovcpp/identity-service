using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Auth.VerifyEmail;

public sealed class VerifyEmailHandler(IUserRepository users, IEmailVerificationRepository verification,
    IUnitOfWork work, ITokenGenerator tokens, TimeProvider clock, OperationContext operation) : ICommandHandler<VerifyEmailCommand, Unit>
{
    public async Task<Unit> Handle(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var token = await verification.FindByHashAsync(tokens.Hash(command.Token), cancellationToken);
        if (token is null) { throw new InvalidVerificationTokenException(); }
        // All verification operations lock the user first, serializing different tokens for the same user.
        var user = await users.FindByIdForUpdateAsync(token.UserId, cancellationToken);
        var now = clock.GetUtcNow();
        if (user?.Status != UserStatus.PendingVerification || !token.IsUsable(now))
        {
            throw new InvalidVerificationTokenException();
        }
        token.Consume(now);
        user.VerifyEmail(now);
        verification.Add(SecurityAuditEvent.Create(SecurityEventType.VerifyEmail, user.Id, null,
            operation.CorrelationId, new AuditMetadata(), now));
        try
        {
            await work.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            throw new InvalidVerificationTokenException();
        }
        return new Unit();
    }
}
