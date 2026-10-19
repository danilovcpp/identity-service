using System.Data;
using Identity.Application.Abstractions.Persistence;
using Identity.Application.Features.Admin.Audit.ListAuditEvents;
using Identity.Application.Features.Admin.Common;
using Identity.Application.Features.Admin.Dashboard.GetDashboard;
using Identity.Application.Features.Admin.Users.GetUserDetails;
using Identity.Application.Features.Admin.Users.GetUserSessions;
using Identity.Application.Features.Admin.Users.ListUsers;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

internal sealed class AdminReadRepository(IdentityDbContext context) : IAdminReadRepository
{
    public async Task<DashboardResult> GetDashboardAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        now = now.ToUniversalTime();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var start = new DateTimeOffset(today.AddDays(-29).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var end = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        // One snapshot for all aggregates. Only grouped scalars are transferred, never users/session entities.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var counts = await context.Users.AsNoTracking().GroupBy(user => user.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount() }).ToDictionaryAsync(row => row.Status, row => row.Count, cancellationToken);
        var activeSessions = await context.Sessions.AsNoTracking()
            .LongCountAsync(session => session.RevokedAt == null && session.CreatedAt <= now && session.AbsoluteExpiresAt > now, cancellationToken);
        // DateTimeOffset.DateTime translates to AT TIME ZONE 'UTC', independent of the DB session timezone.
        var daily = await context.Users.AsNoTracking().Where(user => user.CreatedAt >= start && user.CreatedAt <= now)
            .GroupBy(user => DateOnly.FromDateTime(user.CreatedAt.DateTime))
            .Select(group => new { Date = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(row => row.Date, row => row.Count, cancellationToken);
        var trend = Enumerable.Range(0, 30).Select(day => today.AddDays(day - 29))
            .Select(date => new RegistrationTrendPoint(date, daily.GetValueOrDefault(date))).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new DashboardResult(counts.Values.Sum(), counts.GetValueOrDefault(UserStatus.Active),
            counts.GetValueOrDefault(UserStatus.PendingVerification), counts.GetValueOrDefault(UserStatus.Invited),
            counts.GetValueOrDefault(UserStatus.Disabled), counts.GetValueOrDefault(UserStatus.Deactivated),
            activeSessions, trend, now, start, end);
    }

    public async Task<PageResult<AdminUserListItem>> ListUsersAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var users = context.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
            users = users.Where(user => EF.Functions.ILike(user.Email, pattern, "\\")
                || (user.DisplayName != null && EF.Functions.ILike(user.DisplayName, pattern, "\\")));
        }
        if (query.Status is not null && AdminReadValidation.TryStatus(query.Status, out var status))
        { users = users.Where(user => user.Status == status); }
        if (query.EmailVerified is { } verified) { users = users.Where(user => (user.EmailVerifiedAt != null) == verified); }
        var ascending = query.SortDirection == "asc";
        var ordered = query.SortBy switch
        {
            "createdAt" => ascending ? users.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id) : users.OrderByDescending(user => user.CreatedAt).ThenByDescending(user => user.Id),
            "email" => ascending ? users.OrderBy(user => user.Email).ThenBy(user => user.Id) : users.OrderByDescending(user => user.Email).ThenByDescending(user => user.Id),
            "status" => ascending ? users.OrderBy(user => user.Status).ThenBy(user => user.Id) : users.OrderByDescending(user => user.Status).ThenByDescending(user => user.Id),
            _ => throw new ArgumentException("Unsupported user sort.", nameof(query))
        };
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var total = await users.LongCountAsync(cancellationToken);
        var items = await ordered.Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize)
            .Select(user => new AdminUserListItem(user.Id, user.Email, user.DisplayName, user.Status.ToString(), user.EmailVerifiedAt != null, user.CreatedAt))
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PageResult<AdminUserListItem>(items, query.Page, query.PageSize, total);
    }

    public Task<AdminUserDetails?> GetUserDetailsAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => new AdminUserDetails(user.Id, user.Email, user.DisplayName, user.Status.ToString(), user.EmailVerifiedAt != null,
                user.EmailVerifiedAt, user.CreatedAt, user.UpdatedAt, user.DeactivatedAt, user.DisabledAt, user.DisabledBy,
                user.DisableReason, user.FailedLoginCount, user.LockedUntil)).SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<AdminUserSession>?> GetUserSessionsAsync(GetUserSessionsQuery query, DateTimeOffset now, CancellationToken cancellationToken)
    {
        now = now.ToUniversalTime();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        if (!await context.Users.AsNoTracking().AnyAsync(user => user.Id == query.UserId, cancellationToken)) { return null; }
        var sessions = context.Sessions.AsNoTracking().Where(session => session.UserId == query.UserId);
        sessions = query.State switch
        {
            "active" => sessions.Where(session => session.RevokedAt == null && session.CreatedAt <= now && session.AbsoluteExpiresAt > now),
            "revoked" => sessions.Where(session => session.RevokedAt != null),
            "expired" => sessions.Where(session => session.RevokedAt == null && session.AbsoluteExpiresAt <= now),
            _ => sessions
        };
        var total = await sessions.LongCountAsync(cancellationToken);
        var items = await sessions.OrderByDescending(session => session.CreatedAt).ThenByDescending(session => session.Id)
            .Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize)
            .Select(session => new AdminUserSession(session.Id, session.ClientId, session.UserAgent, session.CreatedAt,
                session.LastUsedAt, session.AbsoluteExpiresAt, session.RevokedAt, session.RevokeReason == null ? null : session.RevokeReason.ToString(),
                session.RevokedAt == null && session.CreatedAt <= now && session.AbsoluteExpiresAt > now)).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PageResult<AdminUserSession>(items, query.Page, query.PageSize, total);
    }

    public async Task<PageResult<AdminAuditEvent>> ListAuditEventsAsync(ListAuditEventsQuery query, CancellationToken cancellationToken)
    {
        var events = context.SecurityAuditEvents.AsNoTracking();
        if (query.UserId is { } userId) { events = events.Where(audit => audit.UserId == userId || audit.TargetUserId == userId); }
        if (query.ActorUserId is { } actorId) { events = events.Where(audit => audit.ActorUserId == actorId); }
        if (query.EventType is { } type) { events = events.Where(audit => audit.EventType == type); }
        if (query.From is { } from && AdminReadValidation.TryUtcInstant(from, out var start)) { events = events.Where(audit => audit.OccurredAt >= start); }
        if (query.To is { } to && AdminReadValidation.TryUtcInstant(to, out var end)) { events = events.Where(audit => audit.OccurredAt < end); }
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var total = await events.LongCountAsync(cancellationToken);
        var items = await events.OrderByDescending(audit => audit.OccurredAt).ThenByDescending(audit => audit.Id)
            .Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize)
            .Select(audit => new AdminAuditEvent(audit.Id, audit.UserId, audit.ActorUserId, audit.TargetUserId,
                audit.EventType, audit.OccurredAt, audit.ClientId, audit.CorrelationId)).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PageResult<AdminAuditEvent>(items, query.Page, query.PageSize, total);
    }
}
