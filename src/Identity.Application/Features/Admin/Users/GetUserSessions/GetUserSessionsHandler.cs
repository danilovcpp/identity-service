using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.GetUserSessions;

public sealed class GetUserSessionsHandler(IAdminReadRepository reads, TimeProvider clock) : IQueryHandler<GetUserSessionsQuery, PageResult<AdminUserSession>>
{
    public async Task<PageResult<AdminUserSession>> Handle(GetUserSessionsQuery query, CancellationToken cancellationToken) =>
        await reads.GetUserSessionsAsync(query, clock.GetUtcNow(), cancellationToken) ?? throw new AdminUserNotFoundException();
}
