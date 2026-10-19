using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.EnableUser;

public sealed class EnableUserValidator : ICommandValidator<EnableUserCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(EnableUserCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(AdminWriteValidation.Identifiers(command.ActorId, command.ActorSessionId, command.UserId));
    }
}
