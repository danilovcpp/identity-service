using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.GetUserDetails;

public sealed class GetUserDetailsValidator : IQueryValidator<GetUserDetailsQuery>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(GetUserDetailsQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<FieldValidationError>();
        AdminReadValidation.Identifier(query.UserId, errors);
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(errors);
    }
}
