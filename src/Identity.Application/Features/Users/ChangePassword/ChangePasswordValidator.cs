using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;

namespace Identity.Application.Features.Users.ChangePassword;

public sealed class ChangePasswordValidator(IPasswordPolicy policy) : ICommandValidator<ChangePasswordCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<FieldValidationError>>([.. PasswordInput.Validate(command.CurrentPassword, "currentPassword"),
            .. policy.Validate(command.NewPassword).Select(error => error with { Field = "newPassword" })]);
    }
}
