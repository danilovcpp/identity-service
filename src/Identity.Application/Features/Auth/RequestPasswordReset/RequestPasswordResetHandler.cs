using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Features.Auth.RequestPasswordReset;

public sealed class RequestPasswordResetHandler(IUserRepository users, IPasswordResetRepository resets, IUnitOfWork work,
    ITokenGenerator tokens, IPasswordResetEmailFactory emails, IPasswordResetSettings settings, TimeProvider clock)
    : ICommandHandler<RequestPasswordResetCommand, Unit>
{
    public async Task<Unit> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) { return new Unit(); }
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByNormalizedEmailForUpdateAsync(EmailAddress.Normalize(command.Email), cancellationToken);
        var now = clock.GetUtcNow();
        var raw = tokens.Generate();
        // Do the same token generation/encryption for unknown and ineligible accounts.
        var message = emails.Create(user?.Email ?? EmailAddress.Canonicalize(command.Email), raw, now);
        if (user?.Status == UserStatus.Active)
        {
            await resets.InvalidateOutstandingAsync(user.Id, now, null, cancellationToken);
            resets.Add(OneTimeToken.Create(user.Id, OneTimeTokenPurpose.PasswordReset, tokens.Hash(raw), now, now.AddMinutes(30)));
            resets.Add(message);
            await work.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
