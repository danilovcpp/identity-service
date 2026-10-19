using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Admin.Dashboard.GetDashboard;

public sealed record GetDashboardQuery : IQuery<DashboardResult>;

public sealed record RegistrationTrendPoint(DateOnly Date, long Count);

public sealed record DashboardResult(long TotalUsers, long ActiveUsers, long PendingVerificationUsers,
    long InvitedUsers, long DisabledUsers, long DeactivatedUsers, long ActiveSessions,
    IReadOnlyList<RegistrationTrendPoint> RegistrationTrend, DateTimeOffset AsOf,
    DateTimeOffset RegistrationTrendFrom, DateTimeOffset RegistrationTrendTo);
