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
        if (maximum is > 0 and <= 16384)
        {
            // Bound chunked/unknown-length requests too, including servers other than Kestrel.
            var buffer = new byte[(int)maximum.Value + 1];
            var count = 0;
            while (count < buffer.Length)
            {
                var read = await context.Request.Body.ReadAsync(buffer.AsMemory(count), context.RequestAborted);
                if (read == 0) { break; }
                count += read;
            }
            if (count > maximum)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                var problem = new ProblemDetails { Status = 413, Title = "Request body too large" };
                problem.Extensions["code"] = "request_too_large";
                await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
                Array.Clear(buffer);
                return;
            }
            var original = context.Request.Body;
            using var body = new MemoryStream(buffer, 0, count, writable: false);
            context.Request.Body = body;
            try { await next(context); }
            finally { context.Request.Body = original; Array.Clear(buffer); }
            return;
        }
        await next(context);
    }
}
