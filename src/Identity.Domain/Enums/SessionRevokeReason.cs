namespace Identity.Domain.Enums;

public enum SessionRevokeReason : short
{
    Logout = 0,
    LogoutAll = 1,
    UserRevoked = 2,
    RefreshReplay = 3,
    PasswordChanged = 4,
    PasswordReset = 5,
    AccountDeactivated = 6,
    AccountDisabled = 7,
    AdministratorRevoked = 8
}
