using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class DiscoveryController(JwtKeyRing keys) : ControllerBase
{
    [HttpGet("/.well-known/jwks.json")]
    [ProducesResponseType<JwksResponse>(StatusCodes.Status200OK)]
    public ActionResult<JwksResponse> Jwks()
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(new JwksResponse(keys.GetPublicKeys()));
    }
}

public sealed record JwksResponse(IReadOnlyCollection<PublicJwk> Keys);
