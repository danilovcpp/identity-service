using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.ListUsers;

public sealed class ListUsersHandler(IAdminReadRepository reads) : IQueryHandler<ListUsersQuery, PageResult<AdminUserListItem>>
{
    public Task<PageResult<AdminUserListItem>> Handle(ListUsersQuery query, CancellationToken cancellationToken) =>
        reads.ListUsersAsync(query, cancellationToken);
}
