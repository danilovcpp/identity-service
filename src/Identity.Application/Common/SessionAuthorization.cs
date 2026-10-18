using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Common;

public static class SessionAuthorization
{
    public static void RequireActive(User? user, Session? session, DateTimeOffset now)
    {
        if (user?.Status != UserStatus.Active || session is null || session.UserId != user.Id || !session.IsActive(now) || session.Audience is null)
        {
            throw new UnauthorizedSessionException();
        }
    }
}
