using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.RequestPasswordReset;

public sealed class RequestAdminPasswordResetValidator : ICommandValidator<RequestAdminPasswordResetCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(RequestAdminPasswordResetCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(AdminWriteValidation.Identifiers(command.ActorId, command.ActorSessionId, command.UserId));
    }
}
