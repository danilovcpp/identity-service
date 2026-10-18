using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;

namespace Identity.Application.Features.Health.GetDatabaseReadiness;

public sealed class GetDatabaseReadinessHandler(IDatabaseReadinessProbe probe) : IQueryHandler<GetDatabaseReadinessQuery, bool>
{
    public Task<bool> Handle(GetDatabaseReadinessQuery query, CancellationToken cancellationToken) =>
        probe.CanConnectAsync(cancellationToken);
}
