using Identity.Application.Abstractions.Persistence;
using Identity.Application.Abstractions.Security;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Configuration;

public static class JwtAuthentication
{
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, IOptions<IdentityClientOptions>, JwtKeyRing, TimeProvider>((options, jwt, clients, keys, clock) =>
            {
                var settings = jwt.Value;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    IgnoreTrailingSlashWhenValidatingAudience = false,
                    ValidAudiences = clients.Value.Clients.Where(client => client.Enabled).Select(client => client.Audience),
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    TryAllIssuerSigningKeys = false,
                    IssuerSigningKeyResolver = (_, _, kid, _) => settings.Enabled ? keys.Resolve(kid) : [],
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidTypes = ["JWT"],
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(settings.ClockSkewSeconds),
                    LogValidationExceptions = false,
                    LifetimeValidator = (notBefore, expires, _, _) => notBefore.HasValue && expires.HasValue
                        && expires > notBefore && notBefore <= clock.GetUtcNow().UtcDateTime.AddSeconds(settings.ClockSkewSeconds)
                        && expires > clock.GetUtcNow().UtcDateTime.AddSeconds(-settings.ClockSkewSeconds)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal!;
                        string? Single(string type)
                        {
                            var values = principal.FindAll(type).Select(claim => claim.Value).ToArray();
                            return values.Length == 1 ? values[0] : null;
                        }
                        var clientId = Single("client_id");
                        var audience = context.HttpContext.RequestServices.GetRequiredService<ILoginSettings>().FindAudience(clientId ?? "");
                        if (Single("token_use") != "access" || !Guid.TryParse(Single("sub"), out var userId)
                            || !Guid.TryParse(Single("sid"), out var sessionId) || !Guid.TryParseExact(Single("jti"), "N", out _)
                            || context.SecurityToken is not JsonWebToken token || !token.TryGetPayloadValue<long>("iat", out _)
                            || audience is null || token.Audiences.Count() != 1 || token.Audiences.Single() != audience
                            || !await context.HttpContext.RequestServices.GetRequiredService<ILoginRepository>()
                                .IsActiveAsync(userId, sessionId, clientId!, audience, clock.GetUtcNow(), context.HttpContext.RequestAborted))
                        {
                            context.Fail("Invalid access token.");
                        }
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await WriteProblem(context.HttpContext, 401, "Unauthorized", "unauthorized");
                    },
                    OnForbidden = context => WriteProblem(context.HttpContext, 403, "Forbidden", "forbidden")
                };
            });
        return services;
    }

    private static async Task WriteProblem(HttpContext context, int status, string title, string code)
    {
        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
