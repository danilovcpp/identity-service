using System.Diagnostics;
using System.Diagnostics.Metrics;
using Identity.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.Middleware;

public sealed class RequestMetrics : IDisposable
{
    private readonly Meter _meter = new("Identity.Http");
    public Counter<long> Completed { get; }
    public Histogram<double> Duration { get; }
    public RequestMetrics()
    {
        Completed = _meter.CreateCounter<long>("identity.http.requests");
        Duration = _meter.CreateHistogram<double>("identity.http.duration", "ms");
    }
    public void Dispose() => _meter.Dispose();
}

public sealed class RequestSecurityMiddleware(RequestDelegate next, IHostEnvironment environment, ILogger<RequestSecurityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, OperationContext operation, RequestMetrics metrics)
    {
        context.TraceIdentifier = operation.CorrelationId;
        context.Response.Headers["X-Correlation-ID"] = operation.CorrelationId;
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.OnStarting(() =>
        {
            // ExceptionHandler clears headers; restore the same correlation/security headers on its response.
            context.Response.Headers["X-Correlation-ID"] = operation.CorrelationId;
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            if (context.Request.Path.StartsWithSegments("/api")) { context.Response.Headers.CacheControl = "no-store"; }
            if (environment.IsProduction() && context.Request.IsHttps)
            {
                context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
            }
            return Task.CompletedTask;
        });
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = operation.CorrelationId });
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (environment.IsProduction() && !context.Request.IsHttps && context.Request.Path != "/health/live" && context.Request.Path != "/health/ready")
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                var problem = new ProblemDetails { Status = 400, Title = "HTTPS is required" };
                problem.Extensions["code"] = "https_required";
                await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
                    new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
                return;
            }
            await next(context);
        }
        finally
        {
            // Only the matched route template: never raw path/query, headers, IDs, IP or request DTOs.
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (Activity.Current is { } activity)
            {
                foreach (var name in new[] { "url.query", "url.full", "http.url", "http.target", "user_agent.original" }) { activity.SetTag(name, null); }
                activity.SetTag("url.path", route);
                activity.DisplayName = route;
            }
            var tags = new TagList { { "route", route }, { "status", context.Response.StatusCode } };
            try
            {
                metrics.Completed.Add(1, tags);
                metrics.Duration.Record(elapsed, tags);
                logger.LogInformation("HTTP completed for route {Route} with status {StatusCode} in {ElapsedMs} ms", route, context.Response.StatusCode, elapsed);
            }
            catch (Exception) { /* Telemetry cannot change a completed HTTP operation. */ }
        }
    }
}
