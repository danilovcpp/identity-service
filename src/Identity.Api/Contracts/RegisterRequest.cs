namespace Identity.Api.Contracts;

public interface IEmailRequest
{
    string? Email { get; }
}

public sealed class RegisterRequest : IEmailRequest
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? DisplayName { get; set; }
    public string? ClientId { get; set; }
}

public sealed class EmailVerificationRequest : IEmailRequest
{
    public string? Email { get; set; }
}

public sealed class VerifyEmailRequest
{
    public string? Token { get; set; }
}

public sealed record VerificationAcceptedResponse(string Message);
