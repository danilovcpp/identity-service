using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;

namespace Identity.Application.Features.Auth.Refresh;

public sealed class RefreshValidator : ICommandValidator<RefreshCommand>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(RefreshCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OpaqueTokenText.IsSupported(command.RefreshToken)) { throw new InvalidRefreshTokenException(); }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>([]);
    }
}
