using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Identity.Api.Middleware;

// Security errors keep one contract even when a caller supplies an unsupported Accept header.
public sealed class JsonProblemDetailsWriter(IOptions<ProblemDetailsOptions> options) : IProblemDetailsWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public bool CanWrite(ProblemDetailsContext context) => true;
    public async ValueTask WriteAsync(ProblemDetailsContext context)
    {
        options.Value.CustomizeProblemDetails?.Invoke(context);
        context.ProblemDetails.Type ??= "about:blank";
        context.HttpContext.Response.StatusCode = context.ProblemDetails.Status ?? 500;
        context.HttpContext.Response.ContentType = "application/problem+json";
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        await JsonSerializer.SerializeAsync(context.HttpContext.Response.Body, context.ProblemDetails,
            context.ProblemDetails.GetType(), JsonOptions, context.HttpContext.RequestAborted);
    }
}
