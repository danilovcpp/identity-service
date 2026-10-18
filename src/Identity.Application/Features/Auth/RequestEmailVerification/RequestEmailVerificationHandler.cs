using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Features.Auth.RequestEmailVerification;

public sealed class RequestEmailVerificationHandler(IUserRepository users, IEmailVerificationRepository verification,
    IUnitOfWork work, ITokenGenerator tokens, IVerificationEmailFactory emails, IRegistrationSettings settings,
    TimeProvider clock) : ICommandHandler<RequestEmailVerificationCommand, Unit>
{
    public async Task<Unit> Handle(RequestEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) { return new Unit(); }
        var rawToken = tokens.Generate();
        var now = clock.GetUtcNow();
        // Build even for an unknown/active user to avoid an encryption-work timing shortcut.
        var message = emails.Create(EmailAddress.Canonicalize(command.Email), rawToken, now);
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByNormalizedEmailForUpdateAsync(EmailAddress.Normalize(command.Email), cancellationToken);
        if (user?.Status == UserStatus.PendingVerification)
        {
            verification.Add(OneTimeToken.Create(user.Id, OneTimeTokenPurpose.EmailVerification, tokens.Hash(rawToken), now, now.AddHours(24)));
            verification.Add(message);
            await work.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new Unit();
    }
}
