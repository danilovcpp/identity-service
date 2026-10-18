using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;

namespace Identity.Application.Features.Auth.VerifyEmail;

public sealed class VerifyEmailValidator : ICommandValidator<VerifyEmailCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (command.Token.Length != 43 || !command.Token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            throw new InvalidVerificationTokenException();
        }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>([]);
    }
}
