using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;

namespace Identity.Application.Features.Admin.Auth.GetMe;

public sealed class GetAdminMeHandler(IAdminRepository administrators) : IQueryHandler<GetAdminMeQuery, AdminProfile>
{
    public async Task<AdminProfile> Handle(GetAdminMeQuery query, CancellationToken cancellationToken) =>
        await administrators.ReadProfileAsync(query.UserId, cancellationToken) ?? throw new UnauthorizedSessionException();
}
