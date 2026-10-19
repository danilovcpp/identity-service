using Identity.Api.Configuration;
using Identity.Api.Contracts;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Users.DeactivateAccount;
using Identity.Application.Features.Users.GetCurrentUser;
using Identity.Application.Features.Users.UpdateProfile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Controllers;

[Authorize]
[RequestSizeLimit(16 * 1024)]
[EnableRateLimiting("public-auth")]
public sealed class UsersController(ICommandDispatcher commands, IQueryDispatcher queries) : ApiController
{
    [HttpGet("me")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfile>> GetCurrent(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await queries.Execute<GetCurrentUserQuery, UserProfile>(new GetCurrentUserQuery(CurrentUserId, CurrentSessionId), cancellationToken));
    }

    [HttpPatch("me")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfile>> Update(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await commands.Execute<UpdateProfileCommand, UserProfile>(
            new UpdateProfileCommand(CurrentUserId, CurrentSessionId, request.DisplayName), cancellationToken));
    }

    [HttpPost("me/deactivate")]
    [ServiceFilter(typeof(AccountRateLimitFilter))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(DeactivateAccountRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<DeactivateAccountCommand, Unit>(
            new DeactivateAccountCommand(CurrentUserId, CurrentSessionId, request.Password ?? ""), cancellationToken);
        return NoContent();
    }
}
