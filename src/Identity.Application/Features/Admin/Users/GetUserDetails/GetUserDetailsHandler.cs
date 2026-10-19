using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;

namespace Identity.Application.Features.Admin.Users.GetUserDetails;

public sealed class GetUserDetailsHandler(IAdminReadRepository reads) : IQueryHandler<GetUserDetailsQuery, AdminUserDetails>
{
    public async Task<AdminUserDetails> Handle(GetUserDetailsQuery query, CancellationToken cancellationToken) =>
        await reads.GetUserDetailsAsync(query.UserId, cancellationToken) ?? throw new AdminUserNotFoundException();
}
