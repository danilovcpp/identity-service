using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Identity.Api.Contracts;
using Identity.Domain.Common;
using Identity.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Identity.Api.Configuration;

public sealed class AccountRateLimitFilter(AccountRateLimiter limiter, IOptions<AuthRateLimitOptions> options) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.ActionArguments.Values.OfType<IEmailRequest>().SingleOrDefault();
        string normalized;
        if (request is not null)
        {
            try { normalized = EmailAddress.Normalize(request.Email ?? ""); }
            catch (DomainException) { await next(); return; }
        }
        else if (Guid.TryParse(context.HttpContext.User.FindFirst("sub")?.Value, out var userId)) { normalized = "user:" + userId.ToString("N"); }
        else { await next(); return; }
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        using var lease = await limiter.Limiter.AcquireAsync(key, 1, context.HttpContext.RequestAborted);
        if (!lease.IsAcquired)
        {
            var retry = lease.TryGetMetadata(MetadataName.RetryAfter, out var duration)
                ? (int)Math.Ceiling(duration.TotalSeconds) : options.Value.WindowSeconds;
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, retry).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var problem = new ProblemDetails { Status = 429, Title = "Too many requests" };
            problem.Extensions["code"] = "rate_limited";
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            context.Result = new ObjectResult(problem) { StatusCode = 429, ContentTypes = { "application/problem+json" } };
            return;
        }
        await next();
    }
}
