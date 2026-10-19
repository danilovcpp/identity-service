using System.Security.Claims;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Admin.Auth.ValidateSession;
using Identity.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security.AdminSession;

public static class AdminCookieAuthentication
{
    public const string Scheme = "IdentityAdminCookie";
    public const string ReadPolicy = "IdentityAdminRead";
    public const string WritePolicy = "IdentityAdmin";
    public const string CookieName = "__Host-Identity.Admin";

    public static IServiceCollection AddAdminCookieAuthentication(this IServiceCollection services)
    {
        services.AddDataProtection().SetApplicationName("Identity.Admin");
        services.AddOptions<KeyManagementOptions>().Configure<IOptions<AdminAccessOptions>, ILoggerFactory>((options, access, logs) =>
        {
            if (access.Value.Enabled)
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(access.Value.DataProtectionKeyPath), logs);
            }
        });
        services.AddScoped<AdminCookieEvents>();
        services.AddAuthentication().AddCookie(Scheme);
        services.AddOptions<CookieAuthenticationOptions>(Scheme).Configure<IOptions<AdminAccessOptions>, TimeProvider>((options, access, clock) =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.Path = "/"; // __Host- prefix requires Path=/, Secure and no Domain.
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = access.Value.AbsoluteLifetime;
            options.SlidingExpiration = false;
            options.TimeProvider = clock;
            options.EventsType = typeof(AdminCookieEvents);
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(ReadPolicy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()
                .RequireRole(nameof(AdministratorRole.IdentityAdministrator), nameof(AdministratorRole.IdentityReadOnly)));
            options.AddPolicy(WritePolicy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()
                .RequireRole(nameof(AdministratorRole.IdentityAdministrator)));
        });
        return services;
    }

    public static ClaimsPrincipal Principal(Guid userId, Guid sessionId, string role) => new(new ClaimsIdentity(
        [new Claim("sub", userId.ToString()), new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sid", sessionId.ToString()), new Claim(ClaimTypes.Role, role)], Scheme));
}

public sealed class AdminCookieEvents(IServiceScopeFactory scopes) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        // Validation commits before MVC runs. Keep its tracked entities out of a subsequent use case's transaction.
        await using var scope = scopes.CreateAsyncScope();
        var validate = scope.ServiceProvider.GetRequiredService<ICommandHandler<ValidateAdminSessionCommand, AdministratorRole?>>();
        var subjects = context.Principal!.FindAll("sub").ToArray();
        var sessions = context.Principal.FindAll("sid").ToArray();
        if (subjects.Length == 1 && sessions.Length == 1 && Guid.TryParse(subjects[0].Value, out var userId)
            && Guid.TryParse(sessions[0].Value, out var sessionId)
            && await validate.Handle(new ValidateAdminSessionCommand(userId, sessionId), context.HttpContext.RequestAborted) is { } role)
        {
            // Always use the current database role, never a stale role from the ticket.
            context.ReplacePrincipal(AdminCookieAuthentication.Principal(userId, sessionId, role.ToString()));
            return;
        }
        context.RejectPrincipal();
        context.Response.Cookies.Delete(AdminCookieAuthentication.CookieName,
            new CookieOptions { Path = "/", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) => WriteProblem(context.HttpContext, 401, "Unauthorized", "unauthorized");
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) => WriteProblem(context.HttpContext, 403, "Forbidden", "forbidden");

    private static async Task WriteProblem(HttpContext context, int status, string title, string code)
    {
        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
