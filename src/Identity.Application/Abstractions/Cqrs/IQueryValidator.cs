using Identity.Application.Common;

namespace Identity.Application.Abstractions.Cqrs;

public interface IQueryValidator<in TQuery>
{
    Task<IReadOnlyList<FieldValidationError>> Validate(TQuery query, CancellationToken cancellationToken);
}
