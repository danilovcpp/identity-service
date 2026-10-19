namespace Identity.Domain.Enums;

public enum OneTimeTokenPurpose : short
{
    EmailVerification = 0,
    PasswordReset = 1,
    Invitation = 2
}
