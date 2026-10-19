using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Admin.Dashboard.GetDashboard;
using Identity.Infrastructure.Security.AdminSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Policy = AdminCookieAuthentication.ReadPolicy)]
public sealed class AdminDashboardController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResult>> Get(CancellationToken cancellationToken) =>
        Ok(await queries.Execute<GetDashboardQuery, DashboardResult>(new GetDashboardQuery(), cancellationToken));
}
