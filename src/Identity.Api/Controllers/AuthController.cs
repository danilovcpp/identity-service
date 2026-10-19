using Identity.Api.Configuration;
using Identity.Api.Contracts;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Auth.AcceptInvitation;
using Identity.Application.Features.Auth.Login;
using Identity.Application.Features.Auth.Logout;
using Identity.Application.Features.Auth.LogoutAll;
using Identity.Application.Features.Auth.Refresh;
using Identity.Application.Features.Auth.Register;
using Identity.Application.Features.Auth.RequestEmailVerification;
using Identity.Application.Features.Auth.RequestPasswordReset;
using Identity.Application.Features.Auth.ResetPassword;
using Identity.Application.Features.Auth.VerifyEmail;
using Identity.Application.Features.Users.ChangePassword;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Controllers;

[EnableRateLimiting("public-auth")]
[RequestSizeLimit(16 * 1024)]
public sealed class AuthController(ICommandDispatcher commands) : ApiController
{
    private static readonly VerificationAcceptedResponse AcceptedResponse = new("If the request is eligible, a verification email will be sent.");
    private static readonly VerificationAcceptedResponse ResetAcceptedResponse = new("If the request is eligible, a password reset email will be sent.");

    [HttpPost("invitations/accept")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AcceptInvitation(AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<AcceptInvitationCommand, Unit>(new(request.Token ?? "", request.NewPassword ?? ""), cancellationToken);
        return NoContent();
    }

    [HttpPost("password/forgot")]
    [AllowAnonymous]
    [ServiceFilter(typeof(AccountRateLimitFilter))]
    [ProducesResponseType<VerificationAcceptedResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<RequestPasswordResetCommand, Unit>(new RequestPasswordResetCommand(request.Email ?? ""), cancellationToken);
        return Accepted(ResetAcceptedResponse);
    }

    [HttpPost("password/reset")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<ResetPasswordCommand, Unit>(new ResetPasswordCommand(request.Token ?? "", request.NewPassword ?? ""), cancellationToken);
        return NoContent();
    }

    [HttpPost("password/change")]
    [Authorize]
    [ServiceFilter(typeof(AccountRateLimitFilter))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<ChangePasswordCommand, Unit>(new ChangePasswordCommand(CurrentUserId, CurrentSessionId,
            request.CurrentPassword ?? "", request.NewPassword ?? ""), cancellationToken);
        return NoContent();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<TokenPairResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenPairResult>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return Ok(await commands.Execute<RefreshCommand, TokenPairResult>(new RefreshCommand(request.RefreshToken ?? ""), cancellationToken));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await commands.Execute<LogoutCommand, Unit>(new LogoutCommand(request.RefreshToken ?? ""), cancellationToken);
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await commands.Execute<LogoutAllCommand, Unit>(new LogoutAllCommand(CurrentUserId, CurrentSessionId), cancellationToken);
        return NoContent();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ServiceFilter(typeof(AccountRateLimitFilter))]
    [ProducesResponseType<TokenPairResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenPairResult>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        var result = await commands.Execute<LoginCommand, TokenPairResult>(new LoginCommand(request.Email ?? "", request.Password ?? "",
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
