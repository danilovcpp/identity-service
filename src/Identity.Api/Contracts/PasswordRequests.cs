namespace Identity.Api.Contracts;

public sealed class ForgotPasswordRequest : IEmailRequest
{
    public string? Email { get; set; }
}

public sealed class ResetPasswordRequest
{
    public string? Token { get; set; }
    public string? NewPassword { get; set; }
}

public sealed class ChangePasswordRequest
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}

public sealed class DeactivateAccountRequest
{
    public string? Password { get; set; }
}
