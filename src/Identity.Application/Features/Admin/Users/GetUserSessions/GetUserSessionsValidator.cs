using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.GetUserSessions;

public sealed class GetUserSessionsValidator : IQueryValidator<GetUserSessionsQuery>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(GetUserSessionsQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<FieldValidationError>();
        AdminReadValidation.Page(query.Page, query.PageSize, errors);
        AdminReadValidation.Identifier(query.UserId, errors);
        if (query.State is not (null or "active" or "revoked" or "expired"))
        { errors.Add(new("state", "state_invalid", "Use active, revoked or expired, or omit state.")); }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(errors);
    }
}
