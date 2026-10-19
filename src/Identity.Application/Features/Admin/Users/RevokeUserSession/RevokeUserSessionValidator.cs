using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.RevokeUserSession;

public sealed class RevokeUserSessionValidator : ICommandValidator<RevokeUserSessionCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(RevokeUserSessionCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = AdminWriteValidation.Identifiers(command.ActorId, command.ActorSessionId, command.UserId);
        AdminReadValidation.Identifier(command.SessionId, errors, "sessionId");
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(errors);
    }
}
