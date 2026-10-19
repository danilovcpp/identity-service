using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Features.Admin.Common;

namespace Identity.Application.Features.Admin.Users.ListUsers;

public sealed class ListUsersQuery : IQuery<PageResult<AdminUserListItem>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Search { get; init; }
    public string? Status { get; init; }
    public bool? EmailVerified { get; init; }
    public string SortBy { get; init; } = "createdAt";
    public string SortDirection { get; init; } = "desc";
}

public sealed record AdminUserListItem(Guid Id, string Email, string? DisplayName, string Status,
    bool EmailVerified, DateTimeOffset CreatedAt);
