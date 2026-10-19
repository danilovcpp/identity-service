using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;

namespace Identity.Application.Features.Users.DeactivateAccount;

public sealed class DeactivateAccountValidator : ICommandValidator<DeactivateAccountCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(DeactivateAccountCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(PasswordInput.Validate(command.Password, "password"));
    }
}
