using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Identity.Api.Configuration;

// Global filter: also protects future admin controllers from accidentally omitting CSRF validation.
public sealed class AdminSecurityFilter(IAntiforgery antiforgery, IOptions<AdminAccessOptions> access) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (!request.Path.StartsWithSegments("/api/v1/admin")) { return; }
        if (!access.Value.Enabled) { Reject(context, 503, "admin_unavailable", "Administrative access is unavailable"); return; }
        if (!request.IsHttps) { Reject(context, 400, "https_required", "HTTPS is required"); return; }
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)) { return; }
        // Header only. Never accept form, query or a cookie as the request token.
        if (!request.Headers.TryGetValue("X-CSRF-TOKEN", out var token) || token.Count != 1 || string.IsNullOrWhiteSpace(token[0]))
        { Reject(context, 400, "csrf_invalid", "Invalid CSRF token"); return; }
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException) { Reject(context, 400, "csrf_invalid", "Invalid CSRF token"); }
    }

    private static void Reject(AuthorizationFilterContext context, int status, string code, string title)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        context.Result = new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
