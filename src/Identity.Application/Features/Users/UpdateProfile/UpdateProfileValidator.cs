using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;

namespace Identity.Application.Features.Users.UpdateProfile;

public sealed class UpdateProfileValidator : ICommandValidator<UpdateProfileCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = command.DisplayName;
        IReadOnlyList<FieldValidationError> errors = value is not null && (string.IsNullOrWhiteSpace(value)
            || value.Trim().Length > 120 || value.Any(char.IsControl))
            ? [new("displayName", "display_name_invalid", "Use at most 120 characters without control characters.")] : [];
        return Task.FromResult(errors);
    }
}
