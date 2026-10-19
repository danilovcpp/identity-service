using System.Diagnostics;
using System.Threading.RateLimiting;
using Identity.Api.Configuration;
using Identity.Api.Dispatching;
using Identity.Api.Middleware;
using Identity.Application;
using Identity.Application.Abstractions.Cqrs;
using Identity.Infrastructure;
using Microsoft.AspNetCore.Mvc;

Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;
var builder = WebApplication.CreateBuilder(args);

// Framework diagnostics may include raw URLs, binding values or exception details.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.HttpOverrides", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter("Npgsql", LogLevel.None);
builder.Services.AddTransportSecurity();
builder.Services.AddSingleton<RequestMetrics>();

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddIdentityAuthentication();
builder.Services.AddScoped<ICommandDispatcher, CommandDispatcher>();
builder.Services.AddScoped<IQueryDispatcher, QueryDispatcher>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOptions<AuthRateLimitOptions>().BindConfiguration("AuthRateLimits")
    .Validate(options => options.IpPermitLimit is >= 1 and <= 10000 && options.AccountPermitLimit is >= 1 and <= 1000
        && options.WindowSeconds is >= 1 and <= 3600, "Public authentication rate limits are invalid.").ValidateOnStart();
builder.Services.AddSingleton<AccountRateLimiter>();
builder.Services.AddScoped<AccountRateLimitFilter>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("public-auth", context =>
    {
        var settings = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthRateLimitOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.IpPermitLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var duration) ? (int)Math.Ceiling(duration.TotalSeconds) : 60;
        context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, retry).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var problem = new ProblemDetails { Status = 429, Title = "Too many requests" };
        problem.Extensions["code"] = "rate_limited";
        await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = problem });
    };
});
builder.Services.AddSingleton<IProblemDetailsWriter, JsonProblemDetailsWriter>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
})
    .ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = ["The request value is invalid."] })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed"
        };
        problem.Extensions["code"] = "validation_failed";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    };
});
builder.Services.AddOpenApi("v1");
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status switch
        {
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
            StatusCodes.Status401Unauthorized => "unauthorized",
            StatusCodes.Status403Forbidden => "forbidden",
            _ => "request_failed"
        });
    };
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<RequestSecurityMiddleware>();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); }
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
app.UseStatusCodePages();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<RequestSizeLimitMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

// Exposes the composition root to WebApplicationFactory without a separate test entry point.
public partial class Program;
