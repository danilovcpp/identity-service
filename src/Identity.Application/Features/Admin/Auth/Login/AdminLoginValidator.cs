using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Domain.Common;
using Identity.Domain.Exceptions;

namespace Identity.Application.Features.Admin.Auth.Login;

public sealed class AdminLoginValidator : ICommandValidator<AdminLoginCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(AdminLoginCommand command, CancellationToken cancellationToken)
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
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(errors);
    }
}
