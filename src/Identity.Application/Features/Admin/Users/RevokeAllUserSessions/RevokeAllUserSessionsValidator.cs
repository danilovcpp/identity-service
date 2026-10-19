using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.RevokeAllUserSessions;

public sealed class RevokeAllUserSessionsValidator : ICommandValidator<RevokeAllUserSessionsCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(RevokeAllUserSessionsCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(AdminWriteValidation.Identifiers(command.ActorId, command.ActorSessionId, command.UserId));
    }
}
