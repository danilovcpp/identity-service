using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Sessions.GetSessions;
using Identity.Application.Features.Sessions.RevokeSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[Authorize]
public sealed class SessionsController(ICommandDispatcher commands, IQueryDispatcher queries) : ApiController
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SessionSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionSummary>>> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await queries.Execute<GetSessionsQuery, IReadOnlyList<SessionSummary>>(
            new GetSessionsQuery(CurrentUserId, CurrentSessionId), cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await commands.Execute<RevokeSessionCommand, Unit>(new RevokeSessionCommand(CurrentUserId, CurrentSessionId, id), cancellationToken);
        return NoContent();
    }
}
