using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Features.Auth.AcceptInvitation;

public sealed class AcceptInvitationHandler(IUserRepository users, ICredentialRepository credentials, IInvitationRepository invitations,
    IAdminRepository administrators, IUnitOfWork work, IInvitationSettings settings, ITokenGenerator tokens,
    IPasswordService passwords, TimeProvider clock, OperationContext operation) : ICommandHandler<AcceptInvitationCommand, Unit>
{
    public async Task<Unit> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) { throw new InvalidInvitationTokenException(); }
        var hash = tokens.Hash(command.Token);
        var owner = await invitations.FindOwnerAsync(hash, cancellationToken);
        if (owner is null) { throw new InvalidInvitationTokenException(); }
        // Expensive production hashing outside row locks; recheck token/state inside the transaction.
        var passwordHash = passwords.Hash(command.NewPassword);
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdForUpdateAsync(owner.Value, cancellationToken);
        var token = await invitations.FindForUpdateAsync(hash, cancellationToken);
        var now = clock.GetUtcNow();
        if (user?.Status != UserStatus.Invited || token is null || token.UserId != user.Id || !token.IsUsable(now)
            || await credentials.FindByUserIdAsync(user.Id, cancellationToken) is not null)
        { throw new InvalidInvitationTokenException(); }
        token.Consume(now);
        user.AcceptInvitation(now);
        credentials.Add(Credential.Create(user.Id, passwordHash, now));
        await invitations.InvalidateOutstandingAsync(user.Id, now, token.Id, cancellationToken);
        administrators.Add(SecurityAuditEvent.Create(SecurityEventType.InvitationAccepted, user.Id, null, operation.CorrelationId,
            new AuditMetadata(), now, user.Id, user.Id));
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
