# User sessions (11.11)

User Details includes a **Sessions** section for both administrative roles, after
the account profile. It reads only the existing per-user endpoint; there is no
global sessions route or new backend contract.

`sessions/api.ts` uses generated OpenAPI types and the central same-origin API
client. GET requests use cancellation and the query key includes user ID, page,
page size and state. The server supplies ordering, pagination (25 rows) and state
filters (`active`, `revoked`, `expired`, or omitted for all). Section state is local
to the mounted account; catalog filters in the detail URL remain unchanged.
Reads project documented fields into cache, validate identifiers/dates/pagination,
and omit unexpected properties. There are independent loading/empty/error/manual
GET retry states and an explicit first-page action for an empty later page.

The standard Ant Design table shows session ID, client ID, optional user-agent,
server activity, created/last-used/absolute-expiry/revoked timestamps and optional
revoke reason. Dates are local with UTC tooltips. Revoked takes precedence; an
active label uses the server's `isActive`. An inactive row is labelled Expired if
its expiration has passed, otherwise Inactive. This is a read snapshot; Refresh
sessions requests current server state. No IP/device or current-browser indicators
are inferred. This API lists **refresh sessions**, not administrator cookie sessions.

Administrator can revoke an active row, with a confirmation identifying its UUID:

- `DELETE /api/v1/admin/users/{userId}/sessions/{sessionId}`;
- only the selected refresh session is affected;
- other refresh and administrator cookie sessions remain active.

**Revoke all sessions** has a separate confirmation and calls:

- `POST /api/v1/admin/users/{userId}/sessions/revoke-all`, without a body;
- all refresh **and administrator cookie sessions** for the account are revoked;
- managing your own account ends your own administrative session as well.

No affected-session count is promised: visible page/filter totals exclude browser
sessions and can become stale. Revoke all remains available even with no listed
refresh sessions, since unlisted administrator sessions may exist. Revocation does
not disable the account or invalidate external JWTs before their expiration.

Both commands require explicit confirmation and existing cookie/CSRF security.
ReadOnly has no mutation controls or Actions column; authorization remains on the
server. Pending prevents cancellation and duplicate submission, with a synchronous
guard, 30-second timeout, unmount cancellation, no optimistic success and no retry.
`204` closes the dialog and invalidates the user's detail/session query family,
dashboard and audit. No other user's detail queries are invalidated. Focus returns
to Refresh sessions after success because the revoked row action can disappear;
Cancel preserves the normal trigger focus behavior.

`404` explains the missing session/account; `409` asks for refreshed state. Network
failure/timeout means uncertain completion and never causes automatic replay.
Global `401`/`403` handling clears protected data. Passwords, tokens and raw server
errors never appear in the UI, browser storage, logs or analytics.

## Checks

From `src/Identity.Admin.Web`:

```powershell
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run build
```

From the root:

```powershell
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
```

Component tests cover all states, nullable fields, safe projection, malformed DTOs,
server filtering/pagination, cancellation, ReadOnly, exact routes/methods, separate
confirmations, double-submit/pending, `204` invalidation and safe failure feedback.
The jsdom ResizeObserver adapter is test-only; browser tests cover actual layout.

Playwright uses real HTTPS/API/PostgreSQL and disposable native sessions: after a
single revoke its refresh fails, another session remains valid, and revoke all
rejects the second refresh and the reader's administrator cookie. Native tokens
stay in the Node test closure, never browser JS/storage or screenshots. ReadOnly
commands receive real `403`. Further tests cover empty server filtering, injected
`403/404/409` and network responses, plus horizontal scroll, modal keyboard/focus
and screenshots at 360/768/1280px. The sessions project runs after reset tests and
before visual checks, so fixture writes do not race earlier baseline assertions.

No dependencies, schema, migrations, production configuration or API changes.
Full audit UI and public account forms remain separate stages 11.12 and 11.13.
