using Identity.Api.Configuration;
using Identity.Api.Contracts;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Features.Admin.Auth.GetMe;
using Identity.Application.Features.Admin.Auth.Login;
using Identity.Application.Features.Admin.Auth.Logout;
using Identity.Infrastructure.Security.AdminSession;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/auth")]
[Authorize(Policy = AdminCookieAuthentication.ReadPolicy)]
[EnableRateLimiting("public-auth")]
[RequestSizeLimit(16 * 1024)]
public sealed class AdminAuthController(ICommandDispatcher commands, IQueryDispatcher queries, IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public ActionResult<AdminCsrfResponse> Csrf() => Ok(new AdminCsrfResponse(antiforgery.GetAndStoreTokens(HttpContext).RequestToken!));

    [HttpPost("login")]
    [AllowAnonymous]
    [ServiceFilter(typeof(AccountRateLimitFilter))]
    [ProducesResponseType<AdminProfile>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminProfile>> Login(AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await commands.Execute<AdminLoginCommand, AdminLoginResult>(new AdminLoginCommand(request.Email ?? "", request.Password ?? ""), cancellationToken);
        await HttpContext.SignInAsync(AdminCookieAuthentication.Scheme,
            AdminCookieAuthentication.Principal(result.Profile.Id, result.SessionId, result.Profile.Role),
            new AuthenticationProperties { IssuedUtc = result.CreatedAt, ExpiresUtc = result.ExpiresAt, IsPersistent = false, AllowRefresh = false });
        return Ok(result.Profile);
    }

    [HttpGet("me")]
    public async Task<ActionResult<AdminProfile>> Me(CancellationToken cancellationToken) =>
        Ok(await queries.Execute<GetAdminMeQuery, AdminProfile>(new GetAdminMeQuery(Guid.Parse(User.FindFirst("sub")!.Value)), cancellationToken));

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await commands.Execute<AdminLogoutCommand, Unit>(new AdminLogoutCommand(Guid.Parse(User.FindFirst("sub")!.Value),
            Guid.Parse(User.FindFirst("sid")!.Value)), cancellationToken);
        await HttpContext.SignOutAsync(AdminCookieAuthentication.Scheme);
        return NoContent();
    }
}
