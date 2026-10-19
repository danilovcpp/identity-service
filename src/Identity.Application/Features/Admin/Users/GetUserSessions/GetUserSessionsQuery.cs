using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.GetUserSessions;

public sealed class GetUserSessionsQuery(Guid userId, int page = 1, int pageSize = 25, string? state = null) : IQuery<PageResult<AdminUserSession>>
{
    public Guid UserId { get; } = userId;
    public int Page { get; } = page;
    public int PageSize { get; } = pageSize;
    public string? State { get; } = state;
}

public sealed record AdminUserSession(Guid Id, string ClientId, string? UserAgent, DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt, DateTimeOffset AbsoluteExpiresAt, DateTimeOffset? RevokedAt, string? RevokeReason, bool IsActive);
