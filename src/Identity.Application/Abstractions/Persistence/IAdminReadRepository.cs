using Identity.Application.Features.Admin.Audit.ListAuditEvents;
using Identity.Application.Features.Admin.Common;
using Identity.Application.Features.Admin.Dashboard.GetDashboard;
using Identity.Application.Features.Admin.Users.GetUserDetails;
using Identity.Application.Features.Admin.Users.GetUserSessions;
using Identity.Application.Features.Admin.Users.ListUsers;

namespace Identity.Application.Abstractions.Persistence;

public interface IAdminReadRepository
{
    Task<DashboardResult> GetDashboardAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<PageResult<AdminUserListItem>> ListUsersAsync(ListUsersQuery query, CancellationToken cancellationToken);
    Task<AdminUserDetails?> GetUserDetailsAsync(Guid userId, CancellationToken cancellationToken);
    Task<PageResult<AdminUserSession>?> GetUserSessionsAsync(GetUserSessionsQuery query, DateTimeOffset now, CancellationToken cancellationToken);
    Task<PageResult<AdminAuditEvent>> ListAuditEventsAsync(ListAuditEventsQuery query, CancellationToken cancellationToken);
}
