# Administrator password reset request (11.10)

User Details offers **Send password reset link** only to `IdentityAdministrator`
for an `Active`, email-verified account. ReadOnly has no reset controls. The server
also checks credentials, current account state, administrative session and role.
An eligible-looking stale profile can therefore still receive `409`.

The standard Ant Design confirmation identifies the recipient and explains that:

- the recipient chooses the password; administrators cannot see or assign it;
- issuing a new link invalidates earlier reset links;
- sessions are revoked when the recipient completes the reset, not on request;
- acceptance for processing does not guarantee email delivery.

Opening or cancelling the dialog makes no HTTP request. Confirmation invokes only
the existing `POST /api/v1/admin/users/{userId}/password-reset`, without a body.
`passwordReset/api.ts` uses the generated contract and central same-origin cookie/
CSRF client. There are no new endpoints, DTOs, packages, migrations or production
settings. The existing backend `PasswordReset:Enabled` and email configuration
must be enabled by the operator; `503 password_reset_unavailable` is explained.

`usePasswordResetCommand` follows the existing mutation conventions: synchronous
duplicate guard, pending controls, 30-second timeout, cancellation on unmount,
no automatic retry, and no optimistic success. Empty `202` closes the confirmation,
shows **Password reset request accepted for processing.**, and invalidates the
audit query family, including the account's latest events. Account status and
sessions do not change just because a link was requested.

`409` asks the administrator to refresh the account. A network error or timeout
explains that completion is uncertain and asks them to check security activity
before manually submitting again. Existing global `401`/`403` handling clears the
protected view; errors never render raw server details. There is no password input,
reset link/token response, browser storage, console logging or analytics event.
The public reset form remains stage **11.13**.

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

The harness enables the existing reset option with a disposable email template;
it starts real PostgreSQL, Mailpit and HTTPS API/Vite, checks OpenAPI drift and
builds the frontend. Production defaults are unchanged. Playwright reset tests run
after account-status tests, before visual tests, so shared fixture mutations do not
race dashboard counts or account lifecycle assertions.

Component tests cover confirmation/cancel, role and state gating, CSRF/body-less
POST, pending/double submission, `202` feedback/audit-only invalidation, errors,
sanitized responses, unmount cancellation and absence of password fields/storage.
Browser tests verify real `202` and refreshed `admin_requested_password_reset`,
Mailpit envelope receipt, real ReadOnly `403`, real concurrent-state `409`, injected
network/forbidden responses, and keyboard/focus/overflow at 360/768/1280 pixels.
Only Mailpit envelope metadata is read; email bodies containing tokens are never
loaded into Playwright. Screenshots are ignored artifacts under `test-results/`.
