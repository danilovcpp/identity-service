# Source discovery and service operations

Snapshot: Identity1.1.0 at a7dbb2f,2026-10-21; stages0–12 including11.1–11.14 complete.
This describes delivered code, not evidence that a production installation exists.

Known source remote: https://github.com/danilovcpp/identity-service.git.
Known checkout on the author's machine: `D:/Space/Personal/identity-service`.
These are discovery hints, not mandatory runtime paths or a guarantee of access.
Prefer the user/current workspace's location or explicit IDENTITY_REPO_PATH. On another
machine locate an authorized checkout; do not assume this Windows path, repository
visibility, deployment host or credentials. Do not fetch secrets while locating sources.

When available, read repository instructions/spec/status before modifying the service.
Current controller/DTO code and generated OpenAPI resolve exact contracts; if spec,
docs and implementation disagree, report the mismatch rather than silently deciding
a new requirement. Old stage notes in docs can describe superseded delivery state.

| Need | Files relative to Identity checkout |
| --- | --- |
| Scope/current implementation | AGENTS.md, Identity_Codex_Spec.md, Identity_Admin_Codex_Addendum_v1.2.md, IMPLEMENTATION_STATUS.md |
| Ordinary REST/JWT contract | docs/api.md; src/Identity.Api/Controllers/AuthController.cs, UsersController.cs, SessionsController.cs; src/Identity.Api/Contracts/; src/Identity.Application/Common/TokenPairResult.cs |
| DTO/type snapshot | src/Identity.Admin.Web/src/api/schema.d.ts; /openapi/v1.json only in Development |
| Service configuration | docs/deployment.md; src/Identity.Api/appsettings.json; deploy/production.env.example |
| Current production deployment | docs/console-production.md; deploy/compose.production.yaml; src/Identity.Admin.Web/nginx/ |
| Admin authorization/API | docs/admin-access.md, docs/admin-read-api.md, docs/admin-write-api.md |
| Local disposable acceptance | scripts/Test-AdminWebSmoke.ps1; tests/Identity.IntegrationTests/; docs/local-demo.md |

## Configure a consuming app

Use the target app's naming conventions for separate configuration values: trusted API
base URL, exact token issuer, expected audience, public clientId and fixed JWKS URL.
No OAuth client_secret is required. Provide reviewed non-secret examples, never values
invented to represent a working environment. Only public verification settings may
belong in frontend configuration; credentials/token stores remain server/OS protected.

The operator enables the client's `Identity:Clients` entry with ClientId/Audience/Enabled
and configures Jwt issuer/key rings. A config entry is not a client registration endpoint
or a user-to-product membership record. Never repurpose a clientId with another audience:
existing sessions retain their original binding and may need login again.

Service startup defaults many features off. Check Jwt, Registration, PasswordReset,
Invitations and AdminAccess flags when relevant;503 is not an excuse to mock success.
Enabling registration/reset/invitations requires real email/outbox/keys and fixed URL
templates. These operator changes are distinct from configuring an application adapter.

## Identity Console is the service operator's boundary

Console: `/admin/*`; admin API: `/api/v1/admin/*`. System roles IdentityAdministrator
and IdentityReadOnly manage Identity globally; they are not a startup's owner/editor
roles and are not included in ordinary access JWTs. A customer-facing app should not
hold an administrator password/cookie or use admin user search as normal authentication.

Console uses separate Secure/HttpOnly/Strict host-only cookies and in-memory CSRF.
`GET /api/v1/admin/auth/csrf`, then X-CSRF-TOKEN on every unsafe call including login/
logout; refetch the token after login/identity changes. Do not confuse this with the
consumer BFF's own session/CSRF. Full admins may read/write, ReadOnly reads only.

Role bootstrap is a protected CLI grant to an already Active verified account, with
last-admin protection and audit. No default admin/password, HTTP role-management,
impersonation or token/password retrieval endpoint exists. Until MFA is delivered,
all Console entry/admin API routes must remain inside VPN/trusted-network restrictions.

## Deployment and validation boundaries

Production Compose publishes only the TLS web edge, with a required private/loopback
bind and fail-closed admin CIDR file; API has no host port and trusts exactly the edge
IP/one forwarding hop. Public account routes still need reachable ingress for their
recipients; the supplied private deployment is not proof of public Internet access.
Do not open all admin routes to make signup/reset links reachable.

JWT private keys, AES outbox keys, SMTP secrets and persistent Data Protection keys
belong to service/operator secret storage, not app source or frontend bundles.
Migrations are a separate deployment action, never automatic API startup. The operator
maintains PostgreSQL, encrypted backups, keys/certificates and email delivery. Check
the current runbook for UID/mount/grant/network requirements instead of copying old
API-only Compose instructions. An integration request does not authorize live role
grants, database migration, service rollout or external deployment.

When changing Identity itself preserve Clean Architecture (Domain standalone;
Application -> Domain; Infrastructure -> Application/Domain; API composition root),
MVC Controllers and custom CQRS. No MediatR, generic repository, ASP.NET Identity
UserManager/IdentityDbContext or application-specific entities/roles. Use real PostgreSQL
for persistence/concurrency evidence and the repository's applicable checks.

For actual Identity changes, commands include dotnet restore/build/test, EF model checks
and applicable smoke scripts. Frontend has typecheck/lint/format/test:run/build.
`pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1 -Production` builds real API/static
images, PostgreSQL/STARTTLS Mailpit, isolated seeds and browser acceptance, then cleans
its resources; it requires Release build, tools, npm dependencies and Chromium.
Use this only when needed, not against a shared database. For consumer-only changes,
test the consuming adapter plus a disposable Identity contract flow as appropriate.

Maintain this skill as protocol guidance: when Identity contracts change, update the
canonical `skills/identity-integration` references and revalidate/reinstall that folder.
Keep the skill portable; do not embed a real installation's environment, secret files,
tokens or passwords. Other agents can read SKILL.md/references directly, but automatic
discovery depends on their own skill-loader configuration.
