using Identity.Application.Common;
using Identity.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Middleware;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case InvalidCredentialsException:
                problem = new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "Invalid credentials" };
                problem.Extensions["code"] = "invalid_credentials";
                break;
            case LoginDisabledException:
                problem = new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable, Title = "Login is unavailable" };
                problem.Extensions["code"] = "login_disabled";
                break;
            case BadHttpRequestException request:
                problem = new ProblemDetails { Status = request.StatusCode, Title = "Invalid request" };
                problem.Extensions["code"] = request.StatusCode == StatusCodes.Status413PayloadTooLarge ? "request_too_large" : "invalid_request";
                break;
            case InvalidVerificationTokenException:
                problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Invalid verification token" };
                problem.Extensions["code"] = "invalid_verification_token";
                break;
            case RegistrationDisabledException:
                problem = new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable, Title = "Registration is unavailable" };
                problem.Extensions["code"] = "registration_disabled";
                break;
            case RequestValidationException validation:
                problem = new ValidationProblemDetails(validation.Errors.GroupBy(error => error.Field)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Message).Distinct().ToArray()))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed"
                };
                problem.Extensions["code"] = "validation_failed";
                break;
            case DomainException domain:
                problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Invalid state" };
                problem.Extensions["code"] = domain.Code;
                break;
            case ConcurrencyConflictException:
                problem = new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "The state changed concurrently" };
                problem.Extensions["code"] = "concurrency_conflict";
                break;
            default:
                problem = new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "An unexpected error occurred" };
                problem.Extensions["code"] = "request_failed";
                // Never log exception messages, request bodies or database error details.
                logger.LogError("Request failed with exception type {ExceptionType}; trace {TraceId}",
                    exception.GetType().Name, httpContext.TraceIdentifier);
                break;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        });
        return true;
    }
}
