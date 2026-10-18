using Identity.Api.Configuration;
using Identity.Api.Contracts;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Auth.Login;
using Identity.Application.Features.Auth.Register;
using Identity.Application.Features.Auth.RequestEmailVerification;
using Identity.Application.Features.Auth.VerifyEmail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Controllers;

[EnableRateLimiting("public-auth")]
[RequestSizeLimit(16 * 1024)]
public sealed class AuthController(ICommandDispatcher commands) : ApiController
{
    private static readonly VerificationAcceptedResponse AcceptedResponse = new("If the request is eligible, a verification email will be sent.");

    [HttpPost("login")]
    [AllowAnonymous]
    [ServiceFilter(typeof(AccountRateLimitFilter))]
    [ProducesResponseType<LoginResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        var result = await commands.Execute<LoginCommand, LoginResult>(new LoginCommand(request.Email ?? "", request.Password ?? "",
            request.ClientId ?? "", Request.Headers.UserAgent.ToString()), cancellationToken);
        return Ok(result);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ServiceFilter(typeof(AccountRateLimitFilter))]
    [ProducesResponseType<VerificationAcceptedResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<RegisterCommand, Unit>(new RegisterCommand(request.Email ?? "", request.Password ?? "",
            request.DisplayName, request.ClientId ?? ""), cancellationToken);
        return Accepted(AcceptedResponse);
    }

    [HttpPost("email/verification-request")]
    [AllowAnonymous]
    [ServiceFilter(typeof(AccountRateLimitFilter))]
    [ProducesResponseType<VerificationAcceptedResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestEmailVerification(EmailVerificationRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<RequestEmailVerificationCommand, Unit>(new RequestEmailVerificationCommand(request.Email ?? ""), cancellationToken);
        return Accepted(AcceptedResponse);
    }

    [HttpPost("email/verify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<VerifyEmailCommand, Unit>(new VerifyEmailCommand(request.Token ?? ""), cancellationToken);
        return NoContent();
    }
}
