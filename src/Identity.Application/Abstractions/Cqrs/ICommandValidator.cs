using Identity.Application.Common;

namespace Identity.Application.Abstractions.Cqrs;

public interface ICommandValidator<in TCommand>
{
    Task<IReadOnlyList<FieldValidationError>> Validate(TCommand command, CancellationToken cancellationToken);
}
