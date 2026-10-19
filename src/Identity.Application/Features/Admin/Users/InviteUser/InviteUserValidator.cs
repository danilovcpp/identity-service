using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;
using Identity.Domain.Common;
using Identity.Domain.Exceptions;

namespace Identity.Application.Features.Admin.Users.InviteUser;

public sealed class InviteUserValidator : ICommandValidator<InviteUserCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(InviteUserCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = AdminWriteValidation.Identifiers(command.ActorId, command.ActorSessionId);
        try { EmailAddress.Normalize(command.Email); }
        catch (DomainException) { errors.Add(new("email", "email_invalid", "Enter a valid email address without control characters.")); }
        if (command.DisplayName is { } name && (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || name.Any(char.IsControl)))
        { errors.Add(new("displayName", "display_name_invalid", "Use at most 120 characters without control characters.")); }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(errors);
    }
}
