using Identity.Application.Common;

namespace Identity.Application.Abstractions.Security;

public interface IPasswordPolicy
{
    IReadOnlyList<FieldValidationError> Validate(string password);
}
