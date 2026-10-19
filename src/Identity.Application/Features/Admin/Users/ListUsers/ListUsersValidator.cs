using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.ListUsers;

public sealed class ListUsersValidator : IQueryValidator<ListUsersQuery>
{
    public Task<IReadOnlyList<FieldValidationError>> Validate(ListUsersQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<FieldValidationError>();
        AdminReadValidation.Page(query.Page, query.PageSize, errors);
        if (query.Search is { } search && (search.Length > 200 || search.Any(char.IsControl)))
        { errors.Add(new("search", "search_invalid", "Search must be at most 200 characters without control characters.")); }
        if (query.Status is not null && !AdminReadValidation.TryStatus(query.Status, out _))
        { errors.Add(new("status", "status_invalid", "Use a supported user status name.")); }
        if (query.SortBy is not ("createdAt" or "email" or "status"))
        { errors.Add(new("sortBy", "sort_invalid", "Sort by createdAt, email or status.")); }
        if (query.SortDirection is not ("asc" or "desc"))
        { errors.Add(new("sortDirection", "sort_invalid", "Sort direction must be asc or desc.")); }
        return Task.FromResult<IReadOnlyList<FieldValidationError>>(errors);
    }
}
