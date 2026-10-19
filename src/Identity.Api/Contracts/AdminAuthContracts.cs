namespace Identity.Api.Contracts;

public sealed class AdminLoginRequest : IEmailRequest
{
    public string? Email { get; init; }
    public string? Password { get; init; }
}

public sealed record AdminCsrfResponse(string Token);
