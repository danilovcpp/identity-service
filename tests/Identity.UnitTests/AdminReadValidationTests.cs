using Identity.Application.Features.Admin.Audit.ListAuditEvents;
using Identity.Application.Features.Admin.Common;
using Identity.Application.Features.Admin.Users.GetUserDetails;
using Identity.Application.Features.Admin.Users.GetUserSessions;
using Identity.Application.Features.Admin.Users.ListUsers;

namespace Identity.UnitTests;

public sealed class AdminReadValidationTests
{
    [Theory]
    [InlineData("page")]
    [InlineData("pageSize")]
    [InlineData("search")]
    [InlineData("control")]
    [InlineData("status")]
    [InlineData("numericStatus")]
    [InlineData("sortBy")]
    [InlineData("sortDirection")]
    [InlineData("overflow")]
    public async Task UserQueryRejectsUnsupportedFiltersAndUnsafePagination(string scenario)
    {
        var query = scenario switch
        {
            "page" => new ListUsersQuery { Page = 0 },
            "pageSize" => new ListUsersQuery { PageSize = 101 },
            "search" => new ListUsersQuery { Search = new string('x', 201) },
            "control" => new ListUsersQuery { Search = "name\r\n" },
            "status" => new ListUsersQuery { Status = "Missing" },
            "numericStatus" => new ListUsersQuery { Status = "1" },
            "sortBy" => new ListUsersQuery { SortBy = "created_at;DROP TABLE users" },
            "sortDirection" => new ListUsersQuery { SortDirection = "DESC NULLS FIRST" },
            _ => new ListUsersQuery { Page = int.MaxValue, PageSize = 100 }
        };
        Assert.NotEmpty(await new ListUsersValidator().Validate(query, CancellationToken.None));
    }

    [Theory]
    [InlineData("2026-10-19T00:00:00Z", true)]
    [InlineData("2026-10-19T06:00:00+06:00", true)]
    [InlineData("2026-10-19T00:00:00.1234567Z", true)]
    [InlineData("2026-10-19T00:00:00", false)]
    [InlineData("2026-10-19", false)]
    [InlineData("19/10/2026", false)]
    [InlineData("2026-02-30T00:00:00Z", false)]
    public void AuditInstantsRequireExplicitOffsetsAndNormalizeToUtc(string text, bool accepted)
    {
        Assert.Equal(accepted, AdminReadValidation.TryUtcInstant(text, out var parsed));
        if (accepted) { Assert.Equal(TimeSpan.Zero, parsed.Offset); Assert.Equal(0, parsed.Hour); }
    }

    [Fact]
    public async Task DefaultQueriesAndPageBoundariesValidateWithoutDiscardingLiteralSearch()
    {
        Assert.Empty(await new ListUsersValidator().Validate(new ListUsersQuery { Page = 1, PageSize = 100, Search = "_%\\", Status = "Invited" }, CancellationToken.None));
        Assert.Empty(await new ListAuditEventsValidator().Validate(new ListAuditEventsQuery
        { From = "2026-10-19T06:00:00+06:00", To = "2026-10-19T00:00:01Z" }, CancellationToken.None));
        Assert.Empty(await new GetUserSessionsValidator().Validate(new GetUserSessionsQuery(Guid.NewGuid()), CancellationToken.None));
        Assert.DoesNotContain("private-search", new ListUsersQuery { Search = "private-search" }.ToString()!);
    }

    [Fact]
    public async Task InvalidIdentifiersRangesAndSessionStatesAreFieldValidationErrors()
    {
        Assert.NotEmpty(await new GetUserDetailsValidator().Validate(new GetUserDetailsQuery(Guid.Empty), CancellationToken.None));
        Assert.Equal(3, (await new GetUserSessionsValidator().Validate(new GetUserSessionsQuery(Guid.Empty, 0, 25, "all"), CancellationToken.None)).Count);
        Assert.Equal(4, (await new ListAuditEventsValidator().Validate(new ListAuditEventsQuery
        {
            UserId = Guid.Empty,
            ActorUserId = Guid.Empty,
            EventType = "login_success' OR 1=1",
            From = "2026-10-19T00:00:00Z",
            To = "2026-10-19T06:00:00+06:00"
        }, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task ReadValidatorsHonorCancellation()
    {
        var token = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ListUsersValidator().Validate(new ListUsersQuery(), token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ListAuditEventsValidator().Validate(new ListAuditEventsQuery(), token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GetUserDetailsValidator().Validate(new GetUserDetailsQuery(Guid.NewGuid()), token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GetUserSessionsValidator().Validate(new GetUserSessionsQuery(Guid.NewGuid()), token));
    }
}
