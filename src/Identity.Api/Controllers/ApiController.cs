using Identity.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiController : ControllerBase
{
    protected Guid CurrentUserId => RequiredIdentifier("sub");
    protected Guid CurrentSessionId => RequiredIdentifier("sid");

    private Guid RequiredIdentifier(string claim) => Guid.TryParse(User.FindFirst(claim)?.Value, out var id) && id != Guid.Empty
        ? id : throw new UnauthorizedSessionException();
}
