# Account status (11.9)

Administrator can Disable/Enable from User Details. ReadOnly has no status controls.
Disable is offered for Active, PendingVerification and Invited; Enable only for Disabled.
Deactivated and unknown statuses have no transition. There are no role-management or
bulk actions. The server remains authoritative, including its last-administrator guard.

## API and mutations

`features/users/accountStatus/api.ts` calls existing endpoints through the shared typed
same-origin cookie/CSRF client, with generated DisableUserRequest fields:

| POST                                   | Body       | Success  |
| -------------------------------------- | ---------- | -------- |
| `/api/v1/admin/users/{userId}/disable` | `{reason}` | Empty204 |
| `/api/v1/admin/users/{userId}/enable`  | None       | Empty204 |

`useAccountStatusCommand` uses TanStack Query, an immediate duplicate-submit guard,
30s timeout and AbortController created before mutation scheduling. Unmount cancels
before/during HTTP. Existing defaults prohibit retries and offline queuing and use
gcTime0 for inactive mutations. No optimistic status is assigned.

After204, users.all invalidates catalog, detail and cached session pages (including
inactive pages), plus dashboard and audit prefixes. Active queries refetch; inactive
ones refresh when visited. Authorization cache is not invalidated. A self-disable can
revoke the current server session; subsequent401 uses the existing global session cleanup.
The restored status after Enable comes only from GET details: it can still be Invited or
PendingVerification. Revoked sessions and invalidated one-time links are not restored.

## Confirmation and feedback

Both actions reuse ConfirmAction and the established Ant Design theme. Cancel receives
initial focus; Tab stays inside the modal, Escape/cancel returns focus to the trigger.
Disable identifies the target email and explains revoked refresh/admin sessions, blocked
sign-in/refresh, invalidated outstanding one-time links and downstream JWT expiry lag.
Reason is required, max500 UTF-16 characters, one line without control characters to
match the existing backend validator. Input is trimmed only at submission. Form values
are cleared when the modal closes. Cancel during asynchronous form validation also
prevents the not-yet-started command. The shared control-character predicate is also reused
by the existing invitation form; invitation behavior is unchanged.

Confirmation never sends on open/cancel. Pending disables confirmation, fields and close.
Enable has a separate confirmation without reason and explicitly leaves status restoration
to Identity. Success toast appears only after204.409 last_administrator explains the guard;
other409 asks for a refresh because state changed. Validation400 maps recognized Reason
fields to a fixed safe message. CSRF/404/429 and service errors use safe feedback.
401/403 use existing global login/Forbidden handling and protected cache cleanup.
Network/timeout means the outcome is unknown: refresh before deliberately submitting again.
No automatic POST replay, raw server detail or arbitrary exception messages.

Backend/CQRS/API/OpenAPI/DB/migrations/security policy/dependencies/lockfile/config samples
are unchanged. No passwords, tokens, browser storage, logging or analytics are added.
Operators should keep reasons free of credentials and unnecessary personal information.

## Verification

Run typecheck/lint/format:check/test:run/build from the frontend. From the root,
`pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1` uses actual HTTPS API/Vite and
PostgreSQL17/Mailpit, generated API drift checking and production build.

31 new component/hook tests cover all status/role gates, required/whitespace/length/control
validation, no request on open/cancel, pending/doubles,204,403/409 last-admin/state conflicts,
CSRF/404/429/network, safe errors and cancellation. Inactive session/detail/list/dashboard/
audit queries are seeded to verify invalidation without manufacturing a restored state.
Textarea ResizeObserver stub is test-only; actual responsive behavior is checked in Chromium.

The account-status Playwright project depends on the existing invitation project, after
dashboard baseline assertions. Eight serial scenarios use the disposable reader account
and the invitation fixture. They check real disable/enable204, native sign-in200→401→200,
revocation of the reader's separate admin cookie, retained Invited state after enable,
real last-admin409, ReadOnly/Forbidden, network failure and360/768/1280 keyboard/screenshots.
Reader is re-enabled in the successful lifecycle scenario. All fixture DB data is removed
by the harness; no persistent accounts or external deployment are involved. Forbidden is
injected in its negative browser scenario; actual authorization remains covered by backend
integration tests. The harness enables the existing JWT-login feature with a separate,
ephemeral RSA3072 signing key in the ignored disposable directory; HTTPS uses its own key.
Only fixture settings change, not production defaults or env samples. Keys/settings are
removed/restored in the existing finally cleanup. Browser trace/video are disabled and
tokens are never inspected/printed.

Password reset11.10, sessions UI11.11, full audit11.12, public pages11.13 and production
hosting/hardening12 remain separate stages. MFA/restricted-network requirements are unchanged.
