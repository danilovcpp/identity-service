using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Sessions.GetSessions;

public sealed record GetSessionsQuery(Guid UserId, Guid CurrentSessionId) : IQuery<IReadOnlyList<SessionSummary>>;

public sealed record SessionSummary(Guid Id, string ClientId, DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt,
    DateTimeOffset AbsoluteExpiresAt, string? UserAgent, bool IsCurrent);
