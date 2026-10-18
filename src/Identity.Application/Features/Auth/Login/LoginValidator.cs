using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Domain.Common;
using Identity.Domain.Exceptions;

namespace Identity.Application.Features.Auth.Login;

public sealed class LoginValidator : ICommandValidator<LoginCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(LoginCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<FieldValidationError>();
        try { EmailAddress.Normalize(command.Email); }
        catch (DomainException) { errors.Add(new("email", "email_invalid", "Enter a valid email address without control characters.")); }
        try
        {
            if (PasswordText.Normalize(command.Password).Length == 0) { throw new ArgumentException(); }
        }
        catch (ArgumentException) { errors.Add(new("password", "password_invalid", "A password within the supported size is required.")); }
        if (string.IsNullOrWhiteSpace(command.ClientId) || command.ClientId.Length > 100 || command.ClientId.Any(char.IsControl))
        {
            errors.Add(new("clientId", "client_id_invalid", "A client identifier of at most 100 characters is required."));
        }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(errors);
    }
}
