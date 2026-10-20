# Identity: administrative access — stage 8

The v1.1 addendum extends v1.0. Stage 8 provides domain/schema, bootstrap CLI and
browser authentication. Stage9 adds [read-only administrative API](admin-read-api.md).
Stage10 adds [administrative commands and invitation acceptance](admin-write-api.md).
Stages11–12 deliver React UI and integrated production packaging.
See [Console production deployment](console-production.md)
for the current TLS/CIDR boundary, persistent key mount and complete release procedure.
Earlier stage-specific topology notes below are historical; cookie/CLI contracts remain valid.

## Configuration and boundary

Admin access is **disabled by default**, independent of `Jwt:Enabled`. It does not
require clientId or issue JWT/refresh tokens. Existing Bearer APIs remain compatible.
Create a dedicated durable directory outside source control, writable only by the API
OS identity. Apply migrations separately and configure:

```text
AdminAccess__Enabled=true
AdminAccess__DataProtectionKeyPath=<absolute-path-to-existing-protected-directory>
AdminAccess__IdleTimeoutSeconds=1800
AdminAccess__AbsoluteLifetimeSeconds=28800
```

Limits: minimum 60 seconds, idle <= absolute, idle <= 30 minutes, absolute <= 8 hours.
Missing directory or invalid limits fail startup. All API replicas must share the key
ring and fixed Data Protection application name `Identity.Admin`; never share it with
other applications, JWT or outbox key storage.

**HTTPS is required even in Development.** Use SDK HTTPS with a trusted development
certificate or a TLS proxy. Plain HTTP dev Compose supports existing REST APIs; it does
not enable admin login. Do not weaken Secure cookies. Only configured exact proxy peers
may supply forwarded HTTPS/IP headers. CORS stays disabled; use one origin.

Until MFA exists, restrict **all** `/api/v1/admin/*` routes, including CSRF/login, behind
VPN or equivalent trusted network, and close direct backend access. No proxy/VPN or SPA
is installed in stage 8. Existing production Compose stays admin-disabled unless the
operator supplies a persistent writable key mount, config and restricted TLS ingress.
Integrated UI/container packaging is delivered in later stages.

## Local bootstrap / revoke

First register and verify an account with the existing API and Mailpit/SMTP. Only
**Active + email-verified** users may receive roles. No default password, automatic
first-user promotion or HTTP role-management endpoint exists.

Supply `Database__ConnectionString` through a protected operator environment; never
put credentials in command arguments/history/logs. The trusted runner needs DML on
`users`, `identity_administrators`, `admin_sessions`, `security_audit_events`; DDL belongs
to a separate migration role. CLI does not start HTTP/workers or require JWT/SMTP keys.

```powershell
dotnet run --project src/Identity.AdminCli --configuration Release --no-build -- --help
dotnet run --project src/Identity.AdminCli --configuration Release --no-build -- grant --email operator@example.com
dotnet run --project src/Identity.AdminCli --configuration Release --no-build -- grant --user-id <UUID> --role IdentityReadOnly
dotnet run --project src/Identity.AdminCli --configuration Release --no-build -- revoke --user-id <UUID>
# Publish on a trusted build machine; copy the complete output to a protected operator host.
dotnet publish src/Identity.AdminCli --configuration Release --output artifacts/admin-cli
# Requires ASP.NET Core 10 runtime on the operator host.
dotnet artifacts/admin-cli/Identity.AdminCli.dll grant --user-id <UUID>
```

Use exactly one selector: UUID or canonicalized exact email, never partial search.
Roles: `IdentityAdministrator` (read/write) or `IdentityReadOnly` (read). Grant same role
and revoke absent role are idempotent. Role changes revoke all current admin sessions.
Audit `admin_role_granted`/`admin_role_revoked` records target ID/time/trace. CLI actor
and granted_by are null: an OS operator is not an authenticated Identity user. Attribute
the human operator using protected host logs. Output excludes database exceptions/secrets.

Exit: 0 success/idempotent, 1 infrastructure/config error, 2 arguments, 3 last administrator
protection, 4 missing/ineligible target or invalid state, 5 cancellation.

At least one Active + verified **IdentityAdministrator** must remain; ReadOnly does
not count. Revoke/downgrade and self-deactivation serialize through a transaction-scoped
PostgreSQL advisory lock, then user locks. Direct SQL bypasses these protections/audit.
Recover a forgotten password through existing email reset. To retire a compromised last
admin, grant a second independently verified account first, then revoke the compromised
role. CLI revoke removes admin access, not ordinary native/BFF access. Disable/enable user
commands are documented in stage10; do not confuse role revocation with account blocking.

## HTTP / cookie / CSRF contract

JSON MVC endpoints under `/api/v1/admin/auth`:

| Method | Route | Access / response |
|---|---|---|
| GET | `/csrf` | Anonymous/current admin; `200 {token}` + antiforgery cookie |
| POST | `/login` | Anonymous + CSRF; `{email,password}` -> `200 {id,email,displayName,role}` + auth cookie |
| GET | `/me` | Either role; `200 {id,email,displayName,role}` |
| POST | `/logout` | Either role + CSRF; server revoke + delete auth cookie, `204` |

Use `credentials: 'same-origin'`. Fetch `/csrf`; send the returned token in
`X-CSRF-TOKEN` for **every unsafe method**, including login/logout. Fetch `/csrf` again
**after login or identity change**; anonymous tokens cannot be used as authenticated
tokens. Hold CSRF tokens only in memory. No form/query fallback. Never persist passwords,
cookies or credentials in localStorage/sessionStorage or analytics. JS does not read cookies.

`__Host-Identity.Admin` and `__Host-Identity.Admin.Csrf`: Secure, HttpOnly,
SameSite=Strict, Path=/, no Domain. The auth cookie is a nonpersistent session cookie
protected by ASP.NET Core Data Protection; only IDs/system role, no email/password/JWT.
Absolute ticket expiry never slides; PostgreSQL last_seen_at enforces idle expiry.
Every admin request checks user status/verification, current role, session ownership,
expiry and revocation. The database role replaces the ticket role. Validation has its
own scope/transaction to avoid stale tracked entities in subsequent MVC commands.

`IdentityAdminRead` accepts both roles, `IdentityAdmin` only the full administrator.
Both explicitly use the cookie scheme; Bearer cannot grant admin access, and cookies
cannot authenticate existing Bearer endpoints. Anonymous/invalid/expired cookies give
JSON 401 without redirects; correct credentials without role give 403/no cookie; wrong
credentials, inactive/locked/missing-credential accounts give generic 401. ReadOnly write
policy denial is 403. Invalid/missing CSRF is `400 csrf_invalid`. Disabled anonymous
bootstrap endpoints return `503 admin_unavailable`; authentication may reject protected
routes earlier with 401. All errors use existing ProblemDetails/code/traceId.

Login has IP/account rate limits and shared account failure/lockout counters with public
login. Login/logout/role changes are audited without secrets. Logout/revoke/role change
persist admin-session revocation. Password change/reset, logout-all and deactivation
revoke admin sessions within existing transactions. Status/role changes apply on the next
request; requests already authorized may finish. Downstream JWT exp/skew lag remains.

## Migration, keys and recovery

`20261019020843_AdminSecureAccess` adds two tables, nullable disable metadata and indexed
audit actor/target IDs. Existing rows/enum values remain. Legacy disabled metadata is
unknown/null. `Invited=4`, `Invitation=2` (72-hour max) are used by the
[stage10 invitation flow](admin-write-api.md). Users without credentials are safely rejected.
Do not downgrade after new enum/audit data is written without a reviewed compatibility
plan; Down does not silently delete incompatible rows.

Back up PostgreSQL and Data Protection keys separately with strict OS permissions and
encrypted storage/backups. Persistence **does not ensure encryption at rest**: protect
the volume or configure a supported operator-managed key protector. All replicas need
key-ring read/write access, never a public/world-readable mount. Framework rotation is
90 days by default; retain retired keys for unexpired tickets. If keys leak, restrict
ingress, revoke admin sessions, replace the compromised ring and require fresh login.
Adding a new active key alone does not invalidate old tickets. Lost keys require login
again. Never print XML key content. Secret guard rejects conventional key files/raw
master-key payloads; `secrets/` and `data-protection-keys/` are Git-ignored.

After snapshot restore, add this statement to the production runbook's pre-traffic
session/token invalidation transaction:

```sql
UPDATE admin_sessions SET revoked_at=GREATEST(CURRENT_TIMESTAMP,created_at)
WHERE revoked_at IS NULL;
```

Reconcile restored roles/statuses: old backups may restore revoked privileges. Stage 8
retains admin session rows; expired rows are rejected, not purged. Retention cleanup is
left for later hardening. Existing audit retention runs also in admin-only deployments.

References: Microsoft [cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0),
[antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0),
[Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).
