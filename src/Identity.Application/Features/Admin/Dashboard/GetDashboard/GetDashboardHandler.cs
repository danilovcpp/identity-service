using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;

namespace Identity.Application.Features.Admin.Dashboard.GetDashboard;

public sealed class GetDashboardHandler(IAdminReadRepository reads, TimeProvider clock) : IQueryHandler<GetDashboardQuery, DashboardResult>
{
    public Task<DashboardResult> Handle(GetDashboardQuery query, CancellationToken cancellationToken) =>
        reads.GetDashboardAsync(clock.GetUtcNow(), cancellationToken);
}
