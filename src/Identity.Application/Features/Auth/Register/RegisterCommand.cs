using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Auth.Register;

public sealed class RegisterCommand(string email, string password, string? displayName, string clientId) : ICommand<Unit>
{
    public string Email { get; } = email;
    public string Password { get; } = password;
    public string? DisplayName { get; } = displayName;
    public string ClientId { get; } = clientId;
}
