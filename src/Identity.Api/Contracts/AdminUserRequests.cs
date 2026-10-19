namespace Identity.Api.Contracts;

public sealed class InviteUserRequest : IEmailRequest
{
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
}

public sealed class DisableUserRequest
{
    public string? Reason { get; init; }
}

public sealed class AcceptInvitationRequest
{
    public string? Token { get; init; }
    public string? NewPassword { get; init; }
}
