using Identity.Application.Abstractions.Notifications;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Features.Admin.Common;

public sealed class InvitationIssuer(IInvitationRepository invitations, ITokenGenerator tokens, IInvitationEmailFactory emails, IInvitationSettings settings)
{
    // Caller holds the user lock (or is inserting the user) and owns the use-case transaction.
    public async Task IssueAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await invitations.InvalidateOutstandingAsync(user.Id, now, null, cancellationToken);
        var raw = tokens.Generate();
        invitations.Add(OneTimeToken.Create(user.Id, OneTimeTokenPurpose.Invitation, tokens.Hash(raw), now, now + settings.Lifetime));
        invitations.Add(emails.Create(user.Email, raw, now));
    }
}
