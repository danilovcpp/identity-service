namespace Identity.Domain.Enums;

public enum SecurityEventType
{
    Register,
    VerifyEmail,
    LoginSuccess,
    LoginFailed,
    RefreshRotated,
    RefreshReplayDetected,
    Logout,
    LogoutAll,
    PasswordChanged,
    PasswordReset,
    AccountDeactivated
}
