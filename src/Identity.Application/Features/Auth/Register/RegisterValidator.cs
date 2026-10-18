using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;
using Identity.Domain.Common;
using Identity.Domain.Exceptions;

namespace Identity.Application.Features.Auth.Register;

public sealed class RegisterValidator(IPasswordPolicy passwords) : ICommandValidator<RegisterCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(RegisterCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<FieldValidationError>(passwords.Validate(command.Password));
        try { EmailAddress.Normalize(command.Email); }
        catch (DomainException) { errors.Add(new("email", "email_invalid", "Enter a valid email address without control characters.")); }
        if (string.IsNullOrWhiteSpace(command.ClientId) || command.ClientId.Length > 100 || command.ClientId.Any(char.IsControl))
        {
            errors.Add(new("clientId", "client_id_invalid", "A client identifier of at most 100 characters is required."));
        }
        if (command.DisplayName is not null && (string.IsNullOrWhiteSpace(command.DisplayName)
            || command.DisplayName.Trim().Length > 120 || command.DisplayName.Any(char.IsControl)))
        {
            errors.Add(new("displayName", "display_name_invalid", "Use at most 120 characters without control characters."));
        }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(errors);
    }
}
