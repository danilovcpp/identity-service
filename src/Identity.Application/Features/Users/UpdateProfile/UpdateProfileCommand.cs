using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Users.GetCurrentUser;

namespace Identity.Application.Features.Users.UpdateProfile;

public sealed record UpdateProfileCommand(Guid UserId, Guid CurrentSessionId, string? DisplayName) : ICommand<UserProfile>;
