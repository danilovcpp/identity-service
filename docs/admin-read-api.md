# Identity administrative read API — stage 9

These JSON routes extend [admin authentication](admin-access.md). Both
IdentityAdministrator and IdentityReadOnly use IdentityAdminRead + the existing secure
admin cookie; ordinary Bearer tokens do not authorize these routes. HTTPS, opt-in
AdminAccess, durable Data Protection and restricted network ingress remain required.
No frontend or administrative write commands are included.

| GET endpoint | Result |
|---|---|
| `/api/v1/admin/dashboard` | Counts and 30-day registration trend |
| `/api/v1/admin/users` | Paginated users with filters/sort |
| `/api/v1/admin/users/{userId}` | User details, including inactive/invited users |
| `/api/v1/admin/users/{userId}/sessions` | Paginated retained native/BFF sessions |
| `/api/v1/admin/audit-events` | Paginated security events, including admin actions |

No CSRF header is needed for these GETs. Authentication still validates/touches the
separate admin session; read handlers do not modify users/sessions/audit. No-store,
no-referrer, CSP and safe ProblemDetails apply. Anonymous/invalid/revoked cookie ->401;
policy denial ->403; invalid filters ->400 validation_failed. Absent user details or
sessions ->404 user_not_found; existing user without sessions ->empty200 page. Malformed
UUID syntax does not match the route (404); Guid.Empty is validation400.
Development OpenAPI `/openapi/v1.json` includes all5 routes, typed DTOs and page schemas.

## Pagination / users

All lists return `{items,page,pageSize,total}`. Page is 1-based, default1; pageSize is
1–100, default25. Overflowing int offsets are rejected. Beyond-last page returns empty
items and actual total. Each count/page pair uses one PostgreSQL repeatable-read
snapshot; separate HTTP pages may move under concurrent writes.

| Users parameter | Values / behavior |
|---|---|
| `search` | Max200 chars, no controls; trimmed case-insensitive substring in email/displayName |
| `status` | Exact PendingVerification, Active, Disabled, Deactivated, Invited; numeric enums rejected |
| `emailVerified` | Optional true/false; whether email_verified_at exists |
| `sortBy` | createdAt (default), email, status only |
| `sortDirection` | desc (default) or asc only |

Filters AND together; search matches email OR displayName. `%`, `_`, backslash are
literal characters, not wildcard instructions. Whitespace-only search means no search.
SQL values are parameters, sorts are fixed expressions with same-direction UUID ties.
Email sort follows stored display email/database collation; status uses numeric order0–4.
Items: id, email, displayName, status (enum name), emailVerified, createdAt.

Details add emailVerifiedAt, updatedAt, deactivatedAt, disabledAt, disabledBy,
disableReason, failedLoginCount, lockedUntil. No credentials/hash, normalized email,
tokens or outbox payload is queried/returned. Invited users need no credential row.
Fetch sessions/activity separately with paginated endpoints, rather than embedded lists.

## Dashboard / UTC

DTO fields: totalUsers, activeUsers, pendingVerificationUsers, invitedUsers, disabledUsers,
deactivatedUsers, activeSessions, registrationTrend (`[{date,count}]`), plus asOf,
registrationTrendFrom, registrationTrendTo to identify the measured period.
Counts cover all persisted users in mutually exclusive statuses. activeSessions counts
`sessions`: created_at <= asOf, no revocation, absolute_expires_at > asOf. Separate admin
cookie sessions are excluded; no refresh-token join is required by this metric.

Trend has exactly30 ascending zero-filled UTC calendar days including today. From is
midnight29 days ago; To is next UTC midnight (exclusive). Today's bucket is partial
through inclusive asOf. Future-created rows are excluded from trend, remain in totalUsers.
One TimeProvider instant and one DB snapshot are used. Explicit UTC grouping is independent
of host/DB connection timezone. Timestamps are ISO UTC (Z/+00:00), dates yyyy-MM-dd.
Stage11 UI will display local times with explicit UTC context.

## Sessions / audit

Sessions parameters: page/pageSize, optional state=active|revoked|expired. Omission
returns all retained history. Revoked means revoked_at exists even after expiry; expired
means unrevoked and expiry <= asOf. Active uses the dashboard predicate. Order is
createdAt DESC, id DESC. Items: id, clientId, userAgent, createdAt, lastUsedAt,
absoluteExpiresAt, revokedAt, revokeReason (enum name/null), isActive. No token/hash.
These are user native/BFF families, not admin-cookie sessions. Recent means history
retained by existing cleanup; deleted families are not archived. Foreign-user sessions
are excluded. Revoke commands remain stage10.

Audit parameters: page/pageSize, userId, actorUserId, eventType, from, to (combined AND).
userId matches legacy user_id OR target_user_id; actorUserId matches actor_user_id.
CLI actors may be null. Event names are1–80 lowercase ASCII letters/underscores; unknown
well-formed names yield an empty page. from inclusive, to exclusive, with seconds,
optional fractions and explicit Z or ±HH:mm; offsets normalize to UTC. Naive dates and
reversed/equal ranges are rejected. Order: occurredAt DESC, id DESC. Items: id, userId,
actorUserId, targetUserId, eventType, occurredAt, clientId, correlationId. No arbitrary
metadata/entity graphs. Existing audit retention still applies.

## Curl examples

After stage8 login, use a protected cookie jar from a trusted client. The jar is an
authentication credential: keep it outside Git/logs/shared scripts. All paths/UUIDs below
are placeholders; raw cookies/tokens/passwords never belong in query strings/arguments.

```sh
curl --cookie /protected/path/admin-cookies.txt https://identity.example.com/api/v1/admin/dashboard
curl --cookie /protected/path/admin-cookies.txt --get https://identity.example.com/api/v1/admin/users \
  --data-urlencode 'search=Example User' --data-urlencode 'status=Active' \
  --data-urlencode 'emailVerified=true' --data-urlencode 'sortBy=email' --data-urlencode 'sortDirection=asc'
curl --cookie /protected/path/admin-cookies.txt https://identity.example.com/api/v1/admin/users/00000000-0000-0000-0000-000000000001
curl --cookie /protected/path/admin-cookies.txt --get https://identity.example.com/api/v1/admin/users/00000000-0000-0000-0000-000000000001/sessions \
  --data-urlencode 'state=active' --data-urlencode 'pageSize=25'
curl --cookie /protected/path/admin-cookies.txt --get https://identity.example.com/api/v1/admin/audit-events \
  --data-urlencode 'userId=00000000-0000-0000-0000-000000000001' --data-urlencode 'eventType=login_success' \
  --data-urlencode 'from=2026-10-01T06:00:00+06:00' --data-urlencode 'to=2026-11-01T00:00:00Z'
```

Encode values, especially `+` in offsets. Proxy/access logs must also sanitize queries/PII.

## Persistence / performance

Migration `20261019022952_AdminReadIndexes` adds7 composite B-tree indexes for user sorts,
session order and global/user/event audit order. Two single-column audit time/user
indexes are replaced by prefix-compatible composites. Tables/data/role/actor/target
indexes are preserved. Ordinary transactional DDL can block writers during index builds;
plan a migration window on large tables. No extension/pg_trgm dependency.

DTO-only AsNoTracking projections use fixed SELECT counts: dashboard3, users2, details1,
sessions3 (existence/count/page), audit2. Existing cookie validation has its own DB queries
and last_seen touch. No N+1/unbounded entity loading. GROUP BY transfers at most5 status
and30 day counts. Arbitrary substring ILIKE and deep OFFSET/total counts may scan large
tables; bounded pages do not guarantee indexed substring search. Review trigram/keyset
work only if future production scale needs it. Local5000-row measurements are not an SLA.

References: [Npgsql translations](https://www.npgsql.org/efcore/mapping/translations.html),
[EF efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).
