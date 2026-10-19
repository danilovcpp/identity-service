using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Admin.Audit.ListAuditEvents;
using Identity.Application.Features.Admin.Common;
using Identity.Infrastructure.Security.AdminSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/audit-events")]
[Authorize(Policy = AdminCookieAuthentication.ReadPolicy)]
public sealed class AdminAuditController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageResult<AdminAuditEvent>>> List([FromQuery] ListAuditEventsQuery query, CancellationToken cancellationToken) =>
        Ok(await queries.Execute<ListAuditEventsQuery, PageResult<AdminAuditEvent>>(query, cancellationToken));
}
