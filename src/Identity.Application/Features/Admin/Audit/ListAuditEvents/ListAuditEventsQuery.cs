using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Audit.ListAuditEvents;

public sealed class ListAuditEventsQuery : IQuery<PageResult<AdminAuditEvent>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public Guid? UserId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? EventType { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
}

public sealed record AdminAuditEvent(Guid Id, Guid? UserId, Guid? ActorUserId, Guid? TargetUserId,
    string EventType, DateTimeOffset OccurredAt, string? ClientId, string CorrelationId);
