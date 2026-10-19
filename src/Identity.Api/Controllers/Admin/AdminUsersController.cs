using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Admin.Common;
using Identity.Application.Features.Admin.Users.GetUserDetails;
using Identity.Application.Features.Admin.Users.GetUserSessions;
using Identity.Application.Features.Admin.Users.ListUsers;
using Identity.Infrastructure.Security.AdminSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = AdminCookieAuthentication.ReadPolicy)]
public sealed class AdminUsersController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageResult<AdminUserListItem>>> List([FromQuery] ListUsersQuery query, CancellationToken cancellationToken) =>
        Ok(await queries.Execute<ListUsersQuery, PageResult<AdminUserListItem>>(query, cancellationToken));

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<AdminUserDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDetails>> Details(Guid userId, CancellationToken cancellationToken) =>
        Ok(await queries.Execute<GetUserDetailsQuery, AdminUserDetails>(new GetUserDetailsQuery(userId), cancellationToken));

    [HttpGet("{userId:guid}/sessions")]
    [ProducesResponseType<PageResult<AdminUserSession>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageResult<AdminUserSession>>> Sessions(Guid userId, CancellationToken cancellationToken,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? state = null) =>
        Ok(await queries.Execute<GetUserSessionsQuery, PageResult<AdminUserSession>>(new GetUserSessionsQuery(userId, page, pageSize, state), cancellationToken));
}
