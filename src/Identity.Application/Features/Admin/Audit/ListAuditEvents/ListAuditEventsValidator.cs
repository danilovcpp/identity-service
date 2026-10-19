using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Audit.ListAuditEvents;

public sealed class ListAuditEventsValidator : IQueryValidator<ListAuditEventsQuery>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(ListAuditEventsQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<FieldValidationError>();
        AdminReadValidation.Page(query.Page, query.PageSize, errors);
        if (query.UserId is { } userId) { AdminReadValidation.Identifier(userId, errors); }
        if (query.ActorUserId is { } actorId) { AdminReadValidation.Identifier(actorId, errors, "actorUserId"); }
        if (query.EventType is { } type && (type.Length is < 1 or > 80 || type.Any(character => !char.IsAsciiLetterLower(character) && character != '_')))
        { errors.Add(new("eventType", "event_type_invalid", "Use an event name of at most 80 lowercase ASCII letters/underscores.")); }
        DateTimeOffset? from = null, to = null;
        if (query.From is { } start)
        {
            if (AdminReadValidation.TryUtcInstant(start, out var parsed)) { from = parsed; }
            else { errors.Add(new("from", "time_invalid", "Use an ISO timestamp with seconds and an explicit UTC offset.")); }
        }
        if (query.To is { } end)
        {
            if (AdminReadValidation.TryUtcInstant(end, out var parsed)) { to = parsed; }
            else { errors.Add(new("to", "time_invalid", "Use an ISO timestamp with seconds and an explicit UTC offset.")); }
        }
        if (from is not null && to is not null && from >= to)
        { errors.Add(new("to", "time_range_invalid", "To must be later than from.")); }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(errors);
    }
}
