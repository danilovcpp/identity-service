using Identity.Api.Configuration;
using Identity.Api.Contracts;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Admin.Users.DisableUser;
using Identity.Application.Features.Admin.Users.EnableUser;
using Identity.Application.Features.Admin.Users.InviteUser;
using Identity.Application.Features.Admin.Users.RequestPasswordReset;
using Identity.Application.Features.Admin.Users.ResendInvitation;
using Identity.Application.Features.Admin.Users.RevokeAllUserSessions;
using Identity.Application.Features.Admin.Users.RevokeUserSession;
using Identity.Infrastructure.Security.AdminSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = AdminCookieAuthentication.WritePolicy)]
[EnableRateLimiting("public-auth")]
[ServiceFilter(typeof(AccountRateLimitFilter))]
[RequestSizeLimit(16 * 1024)]
public sealed class AdminUserCommandsController(ICommandDispatcher commands) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid ActorSessionId => Guid.Parse(User.FindFirst("sid")!.Value);

    [HttpPost("invitations")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Invite(InviteUserRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<InviteUserCommand, Unit>(new(ActorId, ActorSessionId, request.Email ?? "", request.DisplayName), cancellationToken);
        return Accepted();
    }

    [HttpPost("{userId:guid}/invitations/resend")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Resend(Guid userId, CancellationToken cancellationToken)
    {
        await commands.Execute<ResendInvitationCommand, Unit>(new(ActorId, ActorSessionId, userId), cancellationToken);
        return Accepted();
    }

    [HttpPost("{userId:guid}/password-reset")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> PasswordReset(Guid userId, CancellationToken cancellationToken)
    {
        await commands.Execute<RequestAdminPasswordResetCommand, Unit>(new(ActorId, ActorSessionId, userId), cancellationToken);
        return Accepted();
    }

    [HttpPost("{userId:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disable(Guid userId, DisableUserRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<DisableUserCommand, Unit>(new(ActorId, ActorSessionId, userId, request.Reason ?? ""), cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Enable(Guid userId, CancellationToken cancellationToken)
    {
        await commands.Execute<EnableUserCommand, Unit>(new(ActorId, ActorSessionId, userId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{userId:guid}/sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSession(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        await commands.Execute<RevokeUserSessionCommand, Unit>(new(ActorId, ActorSessionId, userId, sessionId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:guid}/sessions/revoke-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeAllSessions(Guid userId, CancellationToken cancellationToken)
    {
        await commands.Execute<RevokeAllUserSessionsCommand, Unit>(new(ActorId, ActorSessionId, userId), cancellationToken);
        return NoContent();
    }
}
