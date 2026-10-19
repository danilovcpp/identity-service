using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Audit.ListAuditEvents;

public sealed class ListAuditEventsHandler(IAdminReadRepository reads) : IQueryHandler<ListAuditEventsQuery, PageResult<AdminAuditEvent>>
{
    public Task<PageResult<AdminAuditEvent>> Handle(ListAuditEventsQuery query, CancellationToken cancellationToken) =>
        reads.ListAuditEventsAsync(query, cancellationToken);
}
