using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Middleware;

public sealed class RequestSizeLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problems)
    {
        var maximum = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;
        if (maximum is not null && context.Request.ContentLength is long length && length > maximum)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            var problem = new ProblemDetails { Status = StatusCodes.Status413PayloadTooLarge, Title = "Request body too large" };
            problem.Extensions["code"] = "request_too_large";
            await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
            return;
        }
        // Kestrel's endpoint size limit still bounds bodies without Content-Length.
        await next(context);
    }
}
