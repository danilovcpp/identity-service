using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;

namespace Identity.Application.Features.Sessions.GetSessions;

public sealed class GetSessionsHandler(ISessionRepository sessions, TimeProvider clock)
    : IQueryHandler<GetSessionsQuery, IReadOnlyList<SessionSummary>>
{
    public Task<IReadOnlyList<SessionSummary>> Handle(GetSessionsQuery query, CancellationToken cancellationToken) =>
        sessions.GetActiveAsync(query.UserId, query.CurrentSessionId, clock.GetUtcNow(), cancellationToken);
}
