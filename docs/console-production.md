# Identity 1.1 — Console production deployment and runbook

This is the stage 12 deployment path for the complete standalone Identity product.
Existing REST routes, cookie/CSRF contracts and six migrations are unchanged.
The older API-only topology in [production.md](production.md) is historical;
`deploy/compose.production.yaml` now runs API + Console TLS edge. PostgreSQL 17+
and a trusted SMTP relay remain operator-managed. No external business app is required.

## Topology and prerequisites

One HTTPS origin serves `/admin/*`, `/account/*`, `/api/*` and public JWKS. Only
`identity-admin-web` publishes a port; the API has no host port. API trusts exactly
the configured edge IP and one forwarding hop. Edge replaces incoming forwarding
headers. Health, OpenAPI, `/index.html` and unrelated paths are not publicly routed.
SPA fallback applies only to admin/account routes; upstream API errors remain JSON.
Edge rejections (network ACL, body limit, invalid host, gateway failure) can have an
Nginx error body; clients must handle their HTTP status without assuming a DTO.

Until MFA is separately delivered, bind HTTPS to a private VPN interface (or a
loopback interface reached over a protected tunnel), and allow only actual VPN
client CIDRs in the admin network file. Both the SPA entry routes and **every**
case-insensitive admin API route, including login/CSRF, enforce the same boundary.
The bundled allowlist is empty: **deny all**. Never use the smoke fixture's broad
local allow rule in production. Public account pages do not require an admin session
or an admin CIDR, but must be reachable through your chosen protected network topology.

The supplied edge is the TLS terminator, not a trust-all backend for another proxy.
Do not add real-IP forwarding without separately reviewing the exact trusted hop,
ACL source address and API trust chain. Docker/NAT can collapse client addresses:
verify denied and allowed clients from their real networks before enabling admin.
Never allow a shared NAT gateway if it also represents untrusted clients. Choose
a non-overlapping Docker subnet; do not attach unrelated containers to it.

Requirements: Linux Docker/Compose, reviewed .NET 10 and Node 22 build toolchain,
PostgreSQL, protected SMTP with valid STARTTLS/SSL chain, DNS/certificate matching
the exact public host, and encrypted secret storage/backups. CI/local smoke uses
disposable certificates and Mailpit only, without changing production validation.

## Build and release artifacts

From a reviewed clean checkout, after CI checks:

```sh
docker build --build-arg VCS_REF="$(git rev-parse HEAD)" -t identity:1.1.0 .
docker build --build-arg VCS_REF="$(git rev-parse HEAD)" -t identity-admin-web:1.1.0 src/Identity.Admin.Web
dotnet publish src/Identity.AdminCli -c Release -o artifacts/admin-cli
```

The frontend uses locked `npm ci`, Vite and pinned official Node/Nginx manifests;
the final image contains static files and Nginx, no Node process or build environment.
API build context excludes frontend dependencies. Both runtime images run non-root.
Promoting images to your registry is an explicit operator action. Record both
reviewed image digests, source commit and migration SQL with the release. No automatic
registry publication or deployment is performed by CI. Update pinned base images
through normal patch review; a pin is not an assurance against future vulnerabilities.

## Provision configuration and secrets

1. Copy `deploy/production.env.example` to ignored `.env.production`. Supply image
   digests, a private bind IP, non-overlapping edge subnet and distinct proxy/API IPs.
   The proxy address must match the API's exact trusted peer. Keep `AllowedHosts`
   explicit and include `localhost` only for the internal Docker health probe.
2. Provision persistent RSA 3072-bit signing and independent AES-256 outbox keys
   outside Git/images. Configure production key IDs and retained keys as described
   in [deployment.md](deployment.md). No auto-generated production keys or passwords.
3. Create the persistent Data Protection directory in `ADMIN_KEY_DIRECTORY`, owned
   and writable only by the image's API UID (currently 1654; verify the built image).
   Back it with encrypted storage, encrypted backups and restrictive permissions.
   The bind mount must already exist; Compose cannot silently create it. API replicas
   must share the ring and fixed application name `Identity.Admin`.
4. Store the TLS full certificate chain and private key outside the repository.
   Grant Nginx UID 101 read access only to its TLS mounts, and API UID read access
   only to its RSA/AES files. Plain Compose secrets are bind mounts, not a vault.
5. Copy `deploy/identity-host.conf.example` to a protected configuration directory;
   replace `identity.example` with the exact certificate hostname. The default server
   rejects unknown hosts. Copy `deploy/admin-networks.conf.example`, add real VPN
   CIDRs (`10.8.0.0/24 1;` is an example), and set their absolute file paths in env.
   Both configuration mounts are read-only and must already exist.
6. Set SMTP host/port/TLS/credentials through protected runtime configuration. Point
   invitation/reset templates at this origin's `/account/activate#token={token}` and
   `/account/reset-password#token={token}`. Email verification still uses the base
   JSON verify API and a deliberately configured consuming page or operator flow;
   `/account/verify` is not a supplied route. Configure `Registration` independently.
7. Leave `AdminAccess__Enabled=false` and `Invitations__Enabled=false` until network,
   keys and bootstrap are ready; then explicitly enable each needed capability.
   Never render `docker compose config` or container environments into shared logs;
   use `config --quiet`. Do not enable body/access URL/SMTP protocol logging.

## Migrations, first administrator and launch

Use a separate migration credential on a protected operator runner; supply its
connection through `Database__ConnectionString`, not command arguments. Back up first.
Review and apply all six migrations before starting application traffic:

```sh
dotnet tool restore
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure -c Release --no-build --output migration.sql
dotnet ef database update --project src/Identity.Infrastructure -c Release --no-build
docker compose --env-file .env.production -f deploy/compose.production.yaml config --quiet
docker compose --env-file .env.production -f deploy/compose.production.yaml up -d --wait
docker compose --env-file .env.production -f deploy/compose.production.yaml exec -T api dotnet Identity.Api.dll --health-check
docker compose --env-file .env.production -f deploy/compose.production.yaml exec -T identity-admin-web nginx -t
```

Migrations are never applied automatically by API startup. Runtime DB grants are
documented in [production.md](production.md); use DML, not the schema-owner role.
The API needs read-only `identity_administrators` access and SELECT/INSERT/UPDATE on
`admin_sessions` in addition to the existing seven-table grants. The separate CLI
operator role needs membership writes; do not give these to the HTTP runtime role.

Bootstrap is an operator operation, not a public endpoint or first-user promotion.
Create a real account using the existing registration JSON API and a human-chosen
15–128 Unicode-character password sent only in the request body; verify ownership
using its real email and the JSON verification API. Use a protected client/tool
without request-body logging, shell-history passwords or token-bearing URL query.
If public registration is closed, temporarily enable it only inside the restricted
operator network for bootstrap and close it again after verification. Then, with a
protected CLI DB environment and ASP.NET Core 10 runtime on the operator host:

```sh
dotnet artifacts/admin-cli/Identity.AdminCli.dll grant --user-id <verified-active-user-uuid>
```

No default admin email/password is created by Compose, image or runtime. CLI checks
eligibility and audits grants. Enable admin access, sign in at `/admin/login`, then
grant a second independently controlled verified administrator for recovery.
[Admin access](admin-access.md) documents ReadOnly grant, revoke and last-admin rules.
Only the disposable test harness generates fixture accounts/passwords.

## Acceptance before opening traffic

- From a VPN client: login/logout, dashboard, users, invitation/reset email, status,
  session revocation, audit and both public account flows. Confirm SMTP delivery
  separately: HTTP 202 means queued, not delivered.
- From a denied network: admin page, CSRF/login and mixed-case/encoded admin API
  requests must be denied even with spoofed forwarding headers. Confirm no direct
  API published port, no public health/OpenAPI and no fallback HTML on API errors.
  An untrusted direct API peer cannot supply forwarded HTTPS (400), even when it
  can reach the private container network during a security probe.
- Anonymous API gives JSON 401; ReadOnly writes give 403; missing/wrong CSRF gives
  `400 csrf_invalid`. Cookie is Secure/HttpOnly/Strict, host-only, Path=/; no browser
  storage credentials. Restart API and confirm a valid admin session survives when
  its DB session and protected key ring are retained.
- HTML is `no-store`, real hashed assets immutable; missing assets are not cached.
  CSP is enforced, not report-only: same-origin scripts, no inline scripts/eval,
  inline CSS is intentionally permitted for Ant Design/Recharts runtime styles and
  layout attributes. The pinned portal/scrollbar utilities call dynamicCSS without
  forwarding ConfigProvider's nonce, so a nonce-only style policy breaks scroll
  locking. We retain their supported behavior without patching dependencies or
  intercepting DOM APIs. This is a documented CSS-injection risk tradeoff;
  `script-src` still permits neither inline scripts nor eval. HSTS is one year
  without preload/includeSubDomains.
  JSON API/JWKS responses retain a separate deny-all CSP; the runtime-style allowance
  applies to the SPA, not administrative API responses.
- Check actual public certificate chain/renewal, host firewall/VPN ACLs, operator
  permissions and SMTP identity using the deployment's own infrastructure.

Repeatable local equivalent (PowerShell 7.4, Docker Linux containers, Release build,
installed frontend dependencies/Chromium):

```powershell
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1 -Production
```

It seeds a fresh PostgreSQL/Mailpit fixture, checks OpenAPI against Development API,
then replaces that API with the Production image and Nginx built static image.
Mailpit requires STARTTLS with a scoped disposable trust certificate. Full browser
coverage runs without Vite; denied-source probes use a separate container. Existing
browser privacy guards remain active; served entry/assets are compared by SHA-256
with the verified credential-free bundle. `finally` removes only generated resources;
this script never deploys to external accounts or changes machine certificate trust.
For isolated edge/CSP regression checks use the same command with `-SecurityOnly`;
the default always executes the entire suite. The Production HTTP process uses a
separate non-owner database role; bootstrap/migrations retain their own fixture
connection. On Linux the disposable key bind runs under the non-root CI runner UID
so its existing permissions need not be widened. Production uses the image UID and
operator-provisioned ownership as documented above.

## Operations, rotation and recovery

- Monitor private readiness, auth/replay failures, outbox pending/failed counts,
  maintenance freshness, DB storage, certificate expiry and disk/key mount access.
  Docker health is liveness; `restart: unless-stopped` does not restart unhealthy
  processes by itself. Keep host monitoring and a controlled restart procedure.
- Normal update: encrypted DB + config + RSA/AES + Data Protection backups, reviewed
  migration SQL, separate migration job, matching API/web digests, private smoke,
  then traffic. Keep previous image digests. Roll back images only when schema and
  data remain compatible; never run destructive Down migrations as a release shortcut.
- Rotate RSA with retained public keys and AES with old decrypt keys until backlog
  is drained. TLS renewal: replace protected files and recreate the edge to remount
  atomic replacements, then check chain/expiry and `nginx -t`. Framework Data Protection
  rotation defaults to 90 days; retain keys needed by unexpired cookies. Encrypt
  the volume; persistence alone does not encrypt keys. Lost keys require fresh login.
- Compromised admin: restrict ingress immediately. Grant a second verified admin
  before retiring the last one; CLI `revoke --user-id <uuid>` revokes admin sessions.
  Disable the compromised user through another Administrator to also block ordinary
  login/refresh. Existing downstream JWTs can last until exp (+skew). Reconcile audit.
- Compromised session keys: revoke all admin sessions, replace the compromised key
  ring and force new login. Creating a new active key alone does not invalidate
  old tickets. Do not print key XML or delete unrelated key directories.
- Restore PostgreSQL into an isolated environment and verify migrations/constraints,
  encrypted outbox and keys before admitting traffic. Restore protected key backups
  separately. Apply the session/token invalidation transaction in [production.md](production.md),
  **including `admin_sessions`**, and reconcile restored roles, passwords and statuses
  with incident records: snapshots can resurrect revoked privileges and old passwords.
  Require password recovery where needed; restore is not permission reconciliation.

Residual limits: no MFA, OAuth/OIDC/SSO, global JWT revocation, managed TLS automation,
VPN provisioning or SMTP service is introduced. This stage supplies a reviewed local
release path and CI configuration, not external deployment or security certification.
Accessibility manual-review items remain in the [frontend acceptance report](../src/Identity.Admin.Web/docs/frontend-acceptance.md).

References: [Nginx geo](https://nginx.org/en/docs/http/ngx_http_geo_module.html),
[Nginx request variables](https://nginx.org/en/docs/http/ngx_http_core_module.html#variables),
[Ant Design CSP](https://ant.design/components/config-provider),
[Mailpit STARTTLS](https://mailpit.axllent.org/docs/configuration/smtp/).
