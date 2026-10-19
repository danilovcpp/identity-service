using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Persistence;

namespace Identity.Application.Features.Admin.Auth.Login;

public sealed class AdminLoginCommand(string email, string password) : ICommand<AdminLoginResult>
{
    public string Email { get; } = email;
    public string Password { get; } = password;
}

public sealed record AdminLoginResult(Guid SessionId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, AdminProfile Profile);
