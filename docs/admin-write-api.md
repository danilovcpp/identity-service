# Identity administrative commands — stage 10

These commands extend [admin authentication](admin-access.md) and [read API](admin-read-api.md).
Every administrative command requires a valid `IdentityAdministrator` cookie, HTTPS and
`X-CSRF-TOKEN`. ReadOnly is denied by the backend. Cookie validation is followed by another
actor/role/session validation **inside the mutation transaction**, so revocation between
middleware and handler cannot authorize a write. Identity remains a standalone product;
there is no business role assignment, impersonation or arbitrary credential editing.

## Enable email flows

AdminAccess remains opt-in with its existing persistent key ring and VPN/trusted-network
requirements. Invitation delivery/acceptance has a separate feature flag:

```text
Invitations__Enabled=true
Invitations__LifetimeSeconds=259200
Email__InvitationUrlTemplate=https://identity.example/account/activate#token={token}
PasswordReset__Enabled=true
Email__ResetPasswordUrlTemplate=https://identity.example/account/reset-password#token={token}
Outbox__Enabled=true
```

Default invitation lifetime is72 hours, configurable60–259200 seconds. Invitations are
disabled by default. Enabled invitations require valid SMTP/from, a fragment-only URL and
existing AES outbox keys, even if registration/JWT/password-reset are disabled. Production
also requires the outbox worker, SMTP TLS and non-development key IDs. Admin password-reset
requires PasswordReset enabled; with AdminAccess enabled its reset template must also put
the token solely in `#token={token}`. Invalid settings fail startup. These templates are
fixed server configuration; request redirect URLs are ignored, not interpolated.

Dev Compose exposes INVITATIONS_ENABLED, INVITATION_LIFETIME_SECONDS and INVITATION_URL_TEMPLATE
from .env. It still uses plain HTTP for the existing REST development API: enabling the
invitation flag does not enable admin cookies or provide TLS/key-ring mounts. SDK HTTPS or
a restricted operator proxy and AdminAccess configuration are needed for admin calls.
Activation/reset SPA pages arrive in stage11; stage10 delivers email links and JSON API,
not a placeholder web form. A trusted client can submit the fragment token to the API.

## Endpoints

Prefix `/api/v1/admin`:

| Method | Route | Body / success |
|---|---|---|
| POST | `/users/invitations` | `{email,displayName?}` ->202, empty body |
| POST | `/users/{userId}/invitations/resend` | No body ->202, empty body |
| POST | `/users/{userId}/password-reset` | No body ->202, empty body |
| POST | `/users/{userId}/disable` | `{reason}` ->204 |
| POST | `/users/{userId}/enable` | No body ->204 |
| DELETE | `/users/{userId}/sessions/{sessionId}` | No body ->204 |
| POST | `/users/{userId}/sessions/revoke-all` | No body ->204 |

All actor/session IDs come from the validated cookie, not JSON. Email/name use existing
normalization/validation, names <=120 chars; disable reason is required, <=500 chars,
no control characters. UUIDs must be nonempty. Existing request size and IP/account limits
apply; 429 uses the existing Retry-After contract. These limits are currently shared
AuthRateLimits settings. Requests use credentials same-origin and a fresh identity-bound
CSRF token after login. Never include passwords, refresh/one-time tokens or hashes in
admin request arguments, URLs or responses. SMTP success is not guaranteed by HTTP202.

Errors use ProblemDetails/code/traceId: anonymous/stale cookie401, insufficient role403,
CSRF/validation400, absent user404 user_not_found, absent/foreign session404 session_not_found,
duplicate invite409 admin_email_conflict, ineligible state409 admin_user_conflict,
last active full administrator409 last_administrator. Disabled email feature503
invitations_unavailable/password_reset_unavailable. Errors never disclose token validity
details or passwords. Duplicate email conflict is confined to authorized admin API;
public registration retains generic202.

## Invitation and reset semantics

Invite creates a new Invited user **without credentials**, a hash-only single-use
Invitation token, encrypted whole email outbox row and admin_invited_user audit in one
transaction. Existing email in any state ->409, including concurrent public registration.
Registration cannot overwrite an invited account's password. No administrator receives
the generated token, link or password. Resend is explicit and allowed only for Invited
without credentials; it consumes outstanding invitations and creates a new token/email
atomically. Old queued emails may still arrive under at-least-once SMTP, but old tokens
are unusable. Resend after acceptance ->409.

Public endpoint (no admin cookie/CSRF required, rate-limited):

```text
POST /api/v1/auth/invitations/accept
{ "token": "<from-email-fragment>", "newPassword": "<chosen-by-recipient>" }
204 No Content
```

The existing NFKC/PBKDF2/password policy applies. Acceptance locks the user then token,
checks purpose/expiry/consumption/Invited state/absence of credentials, consumes token,
sets verified email + Active, creates initial credential and audit atomically. It neither
logs in automatically nor grants a system role. Reuse, expiry, disabled/ineligible user,
wrong purpose, future-issued token or existing credentials ->same400 invalid_invitation_token.
Malformed/weak password uses standard400 validation_failed. No tokens/credentials in success
body. Stage11 will read fragment, erase history before POST and persist no token in browser storage.

### Curl contract examples

These use placeholder IDs/CSRF values and a protected cookie jar obtained through the
stage8 login protocol. Supply real secrets only through a trusted client in memory, never
shell history/logs. For acceptance the JSON body is supplied from trusted memory via stdin
(`@-`), rather than a plaintext request file or an echoed password/token.

```sh
curl --cookie /protected/admin-cookies.txt -H 'X-CSRF-TOKEN: <csrf-from-memory>' -H 'Content-Type: application/json' \
  -d '{"email":"user@example.com","displayName":"Example User"}' https://identity.example/api/v1/admin/users/invitations
curl --cookie /protected/admin-cookies.txt -H 'X-CSRF-TOKEN: <csrf-from-memory>' -X POST \
  https://identity.example/api/v1/admin/users/00000000-0000-0000-0000-000000000001/invitations/resend
curl --cookie /protected/admin-cookies.txt -H 'X-CSRF-TOKEN: <csrf-from-memory>' -X POST \
  https://identity.example/api/v1/admin/users/00000000-0000-0000-0000-000000000001/password-reset
curl --cookie /protected/admin-cookies.txt -H 'X-CSRF-TOKEN: <csrf-from-memory>' -H 'Content-Type: application/json' \
  -d '{"reason":"Security review"}' https://identity.example/api/v1/admin/users/00000000-0000-0000-0000-000000000001/disable
curl --cookie /protected/admin-cookies.txt -H 'X-CSRF-TOKEN: <csrf-from-memory>' -X POST \
  https://identity.example/api/v1/admin/users/00000000-0000-0000-0000-000000000001/enable
curl --cookie /protected/admin-cookies.txt -H 'X-CSRF-TOKEN: <csrf-from-memory>' -X DELETE \
  https://identity.example/api/v1/admin/users/00000000-0000-0000-0000-000000000001/sessions/00000000-0000-0000-0000-000000000002
curl --cookie /protected/admin-cookies.txt -H 'X-CSRF-TOKEN: <csrf-from-memory>' -X POST \
  https://identity.example/api/v1/admin/users/00000000-0000-0000-0000-000000000001/sessions/revoke-all
curl -H 'Content-Type: application/json' --data-binary @- https://identity.example/api/v1/auth/invitations/accept
```

Admin reset is only for Active + verified users with credentials, including a temporary
brute-force lock. It reuses the existing30-minute reset token, encrypted outbox and public
password-reset endpoint; older reset tokens are invalidated. Requesting email does not
change the password or revoke sessions. Successful reset consumption revokes both admin
and native/BFF sessions through the existing lifecycle. Pending/Invited/Disabled/Deactivated
or missing credential ->409; an administrator never assigns or learns the new password.

## Disable, enable and sessions

Disable accepts Active/PendingVerification/Invited, saves exact prior state, actor/reason/time,
revokes all native/BFF sessions and refresh tokens plus all admin sessions, and consumes all
outstanding verification/reset/invitation tokens. Existing user access to Identity, login,
refresh and admin-cookie validation then fail. Disabled ->disable is idempotent204, preserving
the original reason/audit. Deactivated ->disable/enable conflicts; deactivation is not reversed.

Enable restores the saved state and clears disable metadata. It does not confirm email,
create credentials, reactivate old sessions/tokens or reset independent brute-force lockout.
Enabling a non-disabled, non-deactivated account is idempotent204. Legacy Disabled rows
without prior state restore Active only if already verified, otherwise PendingVerification.
After re-enabling Pending/Invited, request a fresh verification/invitation link. Audit retains
the actor/target/action history; the free-text reason is stored only on the user while disabled.

The last Active + verified IdentityAdministrator cannot be disabled, revoked/downgraded
through CLI or self-deactivated. ReadOnly does not count as a replacement. Loss-of-eligibility
checks share the existing PostgreSQL advisory transaction lock. Admin writes acquire that
lock, sorted actor/target user rows, then session/token locks; existing public lifecycle
operations use the same user-first order. No manual status SQL bypass is part of the API.

Targeted DELETE only revokes the specified native/BFF family owned by the specified user;
repeat204 preserves one audit event. It does not revoke that user's admin transport cookie.
Revoke-all affects both native/BFF and admin sessions, including the caller when targeting
self. Repeated revoke-all is effective/idempotent for sessions and records the requested
administrative action. Session history is retained for replay detection. No replacement
tokens are issued by revocation. Account state remains unchanged by revoke/reset requests.

**Downstream JWT limitation:** these actions immediately affect future Identity requests,
but an external API validating JWTs autonomously may still accept them until exp (+skew).
No global JWT introspection/revocation or MFA is promised. Already completed requests
cannot be undone; new writes revalidate authority at their transaction boundary.

## Audit, migration and operations

Events: admin_invited_user, admin_resent_invitation, admin_requested_password_reset,
admin_disabled_user, admin_enabled_user, admin_revoked_session, admin_revoked_all_sessions,
invitation_accepted. IDs/time/correlation/constant event type are stored; no raw tokens,
passwords, links or reason text in audit metadata. Admin events carry actor and target;
recipient acceptance uses that recipient as actor/target. Events commit with the operation.
Outbox now accepts invitation in addition to verification/reset and retains encrypted
payload on SMTP failure/retry. Invalidation does not retract already sent email.

Migration `20261019024608_AdminWriteLifecycle` adds nullable status_before_disable and its
check, extends revoke reasons (AccountDisabled=7, AdministratorRevoked=8) and event whitelist.
Existing rows are preserved; legacy prior state is not invented. Apply separately before
new binaries receive traffic. Downgrade after new data requires a reviewed compatibility
plan; old check constraints do not silently discard new events/reasons. Backups/restore
must still reconcile roles/statuses, revoke both session transports and consume one-time
tokens before opening traffic. No external deployment/account actions are automated.
