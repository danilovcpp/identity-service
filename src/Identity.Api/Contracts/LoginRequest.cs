namespace Identity.Api.Contracts;

public sealed class LoginRequest : IEmailRequest
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? ClientId { get; set; }
}
