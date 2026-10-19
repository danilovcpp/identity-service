using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Domain.Common;
using Identity.Domain.Exceptions;

namespace Identity.Application.Features.Auth.RequestPasswordReset;

public sealed class RequestPasswordResetValidator : ICommandValidator<RequestPasswordResetCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { EmailAddress.Normalize(command.Email); }
        catch (DomainException)
        {
            return Task.FromResult<IReadOnlyList<FieldValidationError>>([new("email", "email_invalid", "Enter a valid email address without control characters.")]);
        }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>([]);
    }
}
