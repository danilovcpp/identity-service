namespace Identity.Application.Common;

public sealed record FieldValidationError(string Field, string Code, string Message);
