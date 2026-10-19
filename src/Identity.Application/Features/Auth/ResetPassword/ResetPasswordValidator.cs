using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;

namespace Identity.Application.Features.Auth.ResetPassword;

public sealed class ResetPasswordValidator(IPasswordPolicy policy) : ICommandValidator<ResetPasswordCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OpaqueTokenText.IsSupported(command.Token)) { throw new InvalidPasswordResetTokenException(); }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(policy.Validate(command.NewPassword).Select(error => error with { Field = "newPassword" }).ToArray());
    }
}
