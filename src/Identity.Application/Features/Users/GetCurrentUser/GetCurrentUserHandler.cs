using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;

namespace Identity.Application.Features.Users.GetCurrentUser;

public sealed class GetCurrentUserHandler(IProfileRepository profiles, TimeProvider clock) : IQueryHandler<GetCurrentUserQuery, UserProfile>
{
    public async Task<UserProfile> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken) =>
        await profiles.GetCurrentAsync(query.UserId, query.CurrentSessionId, clock.GetUtcNow(), cancellationToken) ?? throw new UnauthorizedSessionException();
}
