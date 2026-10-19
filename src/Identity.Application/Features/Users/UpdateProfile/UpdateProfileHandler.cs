using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Common;
using Identity.Application.Features.Users.GetCurrentUser;

namespace Identity.Application.Features.Users.UpdateProfile;

public sealed class UpdateProfileHandler(IUserRepository users, ISessionRepository sessions, IUnitOfWork work, TimeProvider clock)
    : ICommandHandler<UpdateProfileCommand, UserProfile>
{
    public async Task<UserProfile> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdForUpdateAsync(command.UserId, cancellationToken);
        var session = await sessions.FindOwnedForUpdateAsync(command.UserId, command.CurrentSessionId, cancellationToken);
        var now = clock.GetUtcNow();
        SessionAuthorization.RequireActive(user, session, now);
        user!.SetDisplayName(command.DisplayName, now);
        await work.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserProfile.FromActive(user);
    }
}
