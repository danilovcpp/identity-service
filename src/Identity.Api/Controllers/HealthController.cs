using Identity.Api.Contracts;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Health.GetDatabaseReadiness;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("health")]
[Produces("application/json")]
public sealed class HealthController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("live")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Live() => Ok(new HealthResponse("healthy"));

    [HttpGet("ready")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Ready(CancellationToken cancellationToken)
    {
        var connected = await queries.Execute<GetDatabaseReadinessQuery, bool>(new GetDatabaseReadinessQuery(), cancellationToken);
        return StatusCode(connected ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable,
            new HealthResponse(connected ? "ready" : "not_ready"));
    }
}
