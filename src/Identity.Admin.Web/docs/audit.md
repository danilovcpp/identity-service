# Audit log (11.12)

`/admin/audit` is a protected, read-only section for both `IdentityAdministrator`
and `IdentityReadOnly`. Audit log is enabled in desktop/mobile navigation. No
export, mutation, arbitrary metadata viewer or additional backend API is added.

## Existing contract

`GET /api/v1/admin/audit-events` supports `Page`, `PageSize`, `ActorUserId`, `UserId`,
`EventType`, `From`, `To`. The server orders by occurredAt and ID descending and
counts/pages the filtered result. The frontend preserves this order and uses
query keys containing every server parameter. Page size defaults to 25, supports
1–100 through the URL, and the page offset is bounded to the backend int32 range.

The paginated audit list and the User Details activity array use separate query
keys (`audit.list(...)` and `audit.userActivity(userId)`). Identical HTTP filters
cannot mix these different cache shapes. Both stay under the audit family so
existing successful mutations invalidate them together.

`UserId` matches `target_user_id` **or** legacy `user_id`; there is no separate
strict-target filter. Accordingly the form says **Target / related user ID**.
The Target column prefers explicit `targetUserId`, otherwise shows `userId` with
**Legacy related user**. Actor uses `actorUserId`. Valid IDs link to User Details;
missing values say Not recorded. Links can lead to a normal 404 for a removed user.

The DTO exposes no metadata. The table uses only id, eventType, occurredAt,
actorUserId, targetUserId, userId and correlationId; unexpected properties and
clientId are not cached. Correlation ID is rendered as text. Metadata is not
invented, fetched separately or rendered from unknown response fields.

Known event names use labels matching the current backend taxonomy, with the raw
code alongside. Unknown events retain their exact code as escaped text. Dates
are local with UTC tooltips through the shared LocalDateTime component. There is
no client-side sorting, filtering or pagination of a single server response.

## Filters and navigation

The Ant Design form applies filters explicitly after UUID/event/range validation.
Event type accepts lowercase ASCII letters and underscores, at most 80 characters,
including valid future/unknown codes. Native datetime-local inputs styled by Ant
Input use local time and millisecond precision. From is inclusive; To is exclusive.
Requests carry ISO instants with explicit UTC offset, never an ambiguous local date.
Invalid calendar dates, local DST gaps and reversed/equal ranges are rejected.
On DST overlaps the browser chooses the offset for the entered local time; the
resulting explicit UTC instant remains visible in the URL.

URL keys: `page`, `pageSize`, `actorUserId`, `userId`, `eventType`, `from`, `to`.
Defaults and unknown/invalid query values are removed by replace canonicalization,
as in the users catalog. An invalid/reversed URL date range is cleared. Form
validation instead keeps invalid draft inputs on screen and makes no request.
Valid explicit-offset URL dates can preserve all seven backend fractional digits;
unchanged form dates retain those original instants. Editing a date uses browser
millisecond precision. IDs are canonical lowercase UUIDs.

Applying/resetting filters resets page to 1. Changing page size also resets the
page. Reload and browser Back/Forward restore filters and form values. The same
allowlist is used for the login return path; unknown secret/query fields, fragments
and external return URLs are discarded. Table links use only validated user IDs.
Browser Back from User Details returns to the original audit URL.

Loading/empty/error/success/refresh states are independent of the filters. GET retry
is explicit, with no automatic retry. A missing later page offers Go to first audit
page. Pagination and filters remain outside the horizontally scrollable table, so
controls work at 360px. No browser storage, console logging or analytics is added.
Existing global401/403 handling and audit-family mutation invalidation remain in use.

## Verification

From `src/Identity.Admin.Web`:

```powershell
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run build
```

From the repository root:

```powershell
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
```

Unit/component coverage includes URL/API casing, safe return paths, int32 boundaries,
calendar/offset/range validation, sub-millisecond URL bounds, both roles, form errors,
history, pagination, loading/zero/error/retry, nulls/links, event labels, HTML escaping,
DTO projection, server order, malformed responses and cancellation.
The jsdom ResizeObserver adapter is test-only.

The audit Playwright project runs after session mutations, before visual checks.
It uses real HTTPS API/PostgreSQL and verifies both roles, row order and original
timestamps against the actual response, user links, every filter, reload/history/
login return, pagination, zero results and responsive keyboard scrolling/screenshots
at 360/768/1280px. Network failure and forbidden response cases use test-only routing.
Screenshots and disposable fixture artifacts stay ignored. No production fallback
fixtures, dependencies, settings, schema or migrations are introduced.
