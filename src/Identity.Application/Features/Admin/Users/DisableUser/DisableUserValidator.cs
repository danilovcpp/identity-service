using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.DisableUser;

public sealed class DisableUserValidator : ICommandValidator<DisableUserCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(DisableUserCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = AdminWriteValidation.Identifiers(command.ActorId, command.ActorSessionId, command.UserId);
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500 || command.Reason.Any(char.IsControl))
        { errors.Add(new("reason", "reason_invalid", "A reason of at most 500 characters without control characters is required.")); }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(errors);
    }
}
