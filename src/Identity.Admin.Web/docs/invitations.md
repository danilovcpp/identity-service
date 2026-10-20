# Invitations (11.8)

IdentityAdministrator can use **Invite user** in the Users page and **Resend invitation**
in an Invited account's details. Both controls are absent for IdentityReadOnly. Backend
authorization remains authoritative; role management, password assignment and public
invitation acceptance pages are outside this substage (implemented separately in
[11.13 — Public account pages](public-account.md)).

## Existing contracts and configuration

`features/users/invitations/api.ts` uses generated OpenAPI types and the existing
same-origin API client, cookie session and memory-only CSRF header:

| Command                                                | Body                     | Success   |
| ------------------------------------------------------ | ------------------------ | --------- |
| POST `/api/v1/admin/users/invitations`                 | `{email,displayName:null | string}`  | Empty 202 |
| POST `/api/v1/admin/users/{userId}/invitations/resend` | None                     | Empty 202 |

No backend, schema, migration, dependency, lockfile or security-policy change is needed.
Enable the existing server invitation configuration and fixed same-origin email template
as described in [admin write API](../../../docs/admin-write-api.md). Delivery uses the
existing encrypted transactional outbox. The UI does not receive an invitation token.
No new browser environment settings are added; frontend `.env.example` stays unchanged.

## Form, confirmation and feedback

The default Ant Design Modal footer submits its vertical Form through a generated form id.
The modal validates required email (up to320 characters) and optional display
name (up to120), rejecting control characters. Submitted values are trimmed; an empty
optional name becomes null. The recipient sets their own password. **Send invitation**
explicitly confirms the request after that explanation; opening the modal sends nothing.
Cancel/close discards form values. Resend uses the shared ConfirmAction and explains that
previous invitation links will stop working. Cancel is initially focused for resend.

Both mutations block double submits with an immediate ref guard and disabled pending
controls. They have a30s timeout, abort when the feature unmounts, and never automatically
retry. Toast feedback appears only after success: **Invitation accepted for processing.**
It does not promise SMTP delivery. A network failure/timeout leaves the outcome unknown;
the UI tells the operator to check Users/activity before a deliberate new request.

400 validation errors map only recognized field names to fixed, safe messages. Duplicate
409 keeps the form and suggests finding the existing account; resend409 instructs a
profile refresh because its state may have changed. 404/429/503/CSRF failures have safe
feedback. Existing global401/403 behavior clears protected data and shows login/Forbidden.
No raw server detail or arbitrary exception text is displayed. No password fields,
browser storage, analytics, logs, raw tokens or invitation links are introduced.

## Cache and layout

`useInvitationCommand` owns the two typed TanStack mutations. Existing defaults disable
retries/offline queuing and garbage-collect inactive mutations immediately. After invite202
it invalidates users and dashboard; after resend202, the specific detail and audit keys.
Active queries refetch; inactive dashboard queries refresh when visited. No page reload or
optimistic account creation. A filtered catalog can legitimately hide the invited account.

The existing theme and responsive action layout are reused. Modal focus trapping includes
form inputs; focus returns to the trigger after close. Form fields are destroyed on close.
No additional confirmation modal or new UI framework is introduced.

## Verification

Run typecheck, lint, format:check, test:run and build from the frontend. From the root run
`pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1` for actual HTTPS API/PostgreSQL/Mailpit.
The harness enables existing Invitations settings only in its disposable environment and
passes a server-only `IDENTITY_E2E_MAILPIT_URL` to Playwright; Vite exposes no env variables.
Mailpit assertions inspect envelope metadata, never bodies containing links/tokens.

Component tests cover validation, optional/null fields,202,400/403/409/429/503/CSRF/network,
readonly/state gating, no request on open/cancel, pending/doubles, invalidations and abort.
`e2e/invitations.spec.ts` covers invite/resend, real duplicate409, actual mail receipts,
Invited row/audit refresh, ReadOnly, network failure and keyboard/focus at360/768/1280px.
The invitation project depends on the existing Chromium suite: shared-database writes run
after dashboard baseline checks, with four workers and no retries retained. Within the
invitation project scenarios run serially against their own uniquely named test account.
All fixture data/processes/volumes/temporary secrets are removed by the harness.
