namespace Identity.Application.Common;

public static class PasswordInput
{
    public static IReadOnlyList<FieldValidationError> Validate(string password, string field)
    {
        try { if (PasswordText.Normalize(password).Length > 0) { return []; } }
        catch (ArgumentException) { }
        return [new(field, "password_invalid", "A password within the supported size is required.")];
    }
}
