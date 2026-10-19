namespace Identity.Domain.Enums;

public enum UserStatus : short
{
    PendingVerification = 0,
    Active = 1,
    Disabled = 2,
    Deactivated = 3,
    Invited = 4
}
