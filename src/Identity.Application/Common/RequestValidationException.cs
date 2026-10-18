namespace Identity.Application.Common;

public sealed class RequestValidationException(IReadOnlyList<FieldValidationError> errors)
    : Exception("Request validation failed.")
{
    public IReadOnlyList<FieldValidationError> Errors { get; } = errors.ToArray();
}
