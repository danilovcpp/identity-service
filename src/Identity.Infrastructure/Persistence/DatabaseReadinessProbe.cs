using Identity.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed class DatabaseReadinessProbe(IdentityDbContext context) : IDatabaseReadinessProbe
{
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connected = await context.Database.CanConnectAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return connected;
    }
}
