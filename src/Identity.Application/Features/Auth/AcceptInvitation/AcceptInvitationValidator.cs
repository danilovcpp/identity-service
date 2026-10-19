using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Abstractions.Security;
using Identity.Application.Common;

namespace Identity.Application.Features.Auth.AcceptInvitation;

public sealed class AcceptInvitationValidator(IPasswordPolicy policy) : ICommandValidator<AcceptInvitationCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OpaqueTokenText.IsSupported(command.Token)) { throw new InvalidInvitationTokenException(); }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(policy.Validate(command.NewPassword).Select(error => error with { Field = "newPassword" }).ToArray());
    }
}
