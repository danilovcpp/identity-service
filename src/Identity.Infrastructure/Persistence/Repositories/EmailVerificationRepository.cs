using Identity.Application.Abstractions.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

internal sealed class EmailVerificationRepository(IdentityDbContext context) : IEmailVerificationRepository
{
    public void Add(OneTimeToken token) => context.OneTimeTokens.Add(token);
    public void Add(OutboxMessage message) => context.OutboxMessages.Add(message);
    public void Add(SecurityAuditEvent auditEvent) => context.SecurityAuditEvents.Add(auditEvent);

    public Task<OneTimeToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.OneTimeTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash
            && token.Purpose == OneTimeTokenPurpose.EmailVerification, cancellationToken);
}
