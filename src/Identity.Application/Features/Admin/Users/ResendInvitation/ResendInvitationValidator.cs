using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.ResendInvitation;

public sealed class ResendInvitationValidator : ICommandValidator<ResendInvitationCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(AdminWriteValidation.Identifiers(command.ActorId, command.ActorSessionId, command.UserId));
    }
}
