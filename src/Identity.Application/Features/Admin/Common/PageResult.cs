namespace Identity.Application.Features.Admin.Common;

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
