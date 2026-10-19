using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Users.ChangePassword;

public sealed class ChangePasswordCommand(Guid userId, Guid currentSessionId, string currentPassword, string newPassword) : ICommand<Unit>
{
    public Guid UserId { get; } = userId;
    public Guid CurrentSessionId { get; } = currentSessionId;
    public string CurrentPassword { get; } = currentPassword;
    public string NewPassword { get; } = newPassword;
}
