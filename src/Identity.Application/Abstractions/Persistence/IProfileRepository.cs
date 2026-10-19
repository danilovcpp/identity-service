using Identity.Application.Features.Users.GetCurrentUser;

namespace Identity.Application.Abstractions.Persistence;

public interface IProfileRepository
{
    Task<UserProfile?> GetCurrentAsync(Guid userId, Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken);
}
