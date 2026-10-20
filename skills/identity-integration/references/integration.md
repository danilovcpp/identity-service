# Consumer integration decisions

Use with protocol.md; adapt mechanisms to the consuming stack rather than prescribing
a universal SDK, schema or framework. Identity 1.1 is a separate HTTP service.

## User authority and multiple startups

Identity owns credentials, canonical email/displayName, verification, global account
status and authentication sessions. The application owns business users/profiles,
membership, onboarding, subscriptions, organizations, permissions and its own data.

Map an authenticated principal to an app-owned row with a unique trusted `(issuer, sub)`
key. Parse the verified Identity `sub` as its UUID; never accept an identity ID/email
from an untrusted request as proof of ownership. A separate local user PK is fine.
Do not create database foreign keys into Identity's database, copy password hashes,
or use email as the permanent join key. Cache/display profile data deliberately;
fetch the current user's Identity profile with its token when needed. There is no
general authenticated user-directory API, profile webhook or user-sync stream to invent.

Create/update the local mapping idempotently after verified authentication, with a
database uniqueness constraint for concurrent first login. A registration's generic
202 contains no user ID and is not evidence that a new account was created.
Do not automatically link legacy users by matching email: migrations/account linking
need an explicit ownership-verification and rollback plan, without exposing credentials.

A shared Identity instance has one email namespace/user directory. A user can log in
to any enabled client; clientId does not grant app membership or restrict registration
to a product. Application membership/entitlement gates remain necessary. Profile changes,
password reset/change, deactivation and logout-all affect the shared identity; own-session
listing/revocation can span clients. Use a separate instance/database/issuer/key rings
when unrelated startups require independent users and lifecycle. Multiple environments
also need explicit issuer/audience/key separation; never point tests at production.

## Browser application: BFF

The browser talks to its own app backend/BFF under a same-origin HTTPS session. The BFF
calls Identity REST APIs using server-side HTTP. This avoids requiring Identity CORS;
do not enable permissive CORS or expose refresh JSON directly to the SPA.

- Give the browser an opaque app session identifier in a Secure, HttpOnly, host-scoped
  cookie with an appropriate SameSite policy and lifetime. Rotate it on login. Do not
  reuse `__Host-Identity.Admin` or forward Identity's administrator cookie.
- Store the Identity token pair in protected durable server-side session storage,
  encrypted at rest with keys separate from data/backups. In-process-only token state
  cannot support durable sessions or multiple BFF replicas. Choose existing session
  infrastructure before introducing a new service or generic auth framework.
- The BFF attaches Bearer access tokens to the appropriate downstream API requests.
  CSRF-protect all cookie-authenticated unsafe BFF routes, including login/logout;
  validate origin as appropriate. SameSite alone is not the full CSRF mechanism.
  Identity's `/admin/auth/csrf` endpoint protects Console, not the startup's BFF.
- Expose only a safe user/session projection to the browser. Avoid putting passwords,
  access/refresh tokens or token-bearing errors into Query caches or client diagnostics.
  Clear protected client state on logout/confirmed invalid session. Distinguish a
  forbidden business action (403) from an invalid authentication session (401).
- Refresh near access expiry or on a specifically expired-token failure, with at most
  one coordinated attempt. Do not turn every 401 into a refresh/login loop. Retry a
  business write only when its idempotency/authorization-failure semantics make it safe.

## Native clients and server/API consumers

Native clients may call the token REST endpoints directly; keep refresh in secure OS
storage and access tokens in memory where practical. Coordinate refresh across any
processes/windows sharing the stored session. Never use plain preferences/files as
the secure store. A trusted backend/BFF uses protected server storage instead.

This is user authentication, not machine credentials: no service-account token grant
or client_credentials flow exists. Do not invent a permanent admin/user password to
authenticate background jobs. Establish a separate authorized mechanism if required.

## Strict refresh protocol

Serialize by **Identity session family**, not a global application lock. For shared
BFF sessions use existing durable distributed coordination/CAS covering read, HTTP
rotation and persistence; a process-local mutex alone cannot protect multiple replicas.

Inside coordination, reload the latest pair/version, determine whether another request
already refreshed it, call refresh once if necessary, validate and atomically store
both returned tokens plus their version/expiry before releasing the lock. Avoid stale
requests overwriting a newer pair. Do not broadcast tokens to client-side tabs as a fix.

If two exchanges use one refresh, the loser causes replay revocation of the winner too.
If the response is lost after commit, or durable persistence of the new pair fails,
the old refresh is unsafe to reuse. Invalidate the local login and require a new login;
optionally make a bounded best-effort idempotent logout of the known family. No automatic
refresh retry/grace period is supported. A transport cancellation cannot undo server commit.

Logout clears the app's local session and requests Identity logout of the corresponding
family. A network failure does not prove server revocation. Password reset/change and
deactivation invalidate all Identity sessions, so the app must end/revalidate its own
session rather than treating an independent BFF cookie as proof of continued access.

## Downstream access JWT verification

Use the consuming framework's standard verifier. Configure a trusted exact issuer,
**this API's audience**, signature/RS256 allowlist, required signed token/expiration,
nbf/exp validation and clock skew at most30 seconds. Validate `token_use=access` and
required `sub`, `sid`, `client_id`, `jti` claims; do not trust decoded-but-unverified data.
Keep claim mapping explicit (.NET: `MapInboundClaims=false`, read `sub`/`sid` directly).
Do not require an invented `at+jwt` header instead of the actual contract.

Fetch public keys from a fixed configured HTTPS `/.well-known/jwks.json`, match kid,
and cache them. Handle planned rotation with bounded timeouts, retained public keys,
and a bounded/cache-coalesced refresh on unknown kid; do not fetch per request, accept
arbitrary header-supplied jku/x5u URLs, or fall back to an untrusted key/algorithm.
The repository's simple one-time JWKS example is illustrative; production consumers
need a key-refresh/availability policy. No private signing key belongs in a verifier.

No OIDC metadata/Authority discovery, introspection or revocation callback exists.
Normal local JWT verification can accept a revoked/disabled account until exp + skew.
If the app needs a stronger guarantee, design an explicit server-side revalidation
policy (Identity `/users/me` checks the current user/session), app-owned blocking or
a separately scoped protocol change; account for outages/latency and cross-client
effects. Do not advertise instant global logout or silently introduce a new endpoint.

## Validate the actual integration

Select checks appropriate to the changed app and stack. Cover successful auth/profile,
anonymous/forbidden paths, app-owned ownership/membership, wrong issuer/audience/alg,
expired/tampered tokens, unknown kid/rotation, single-flight refresh and uncertain
rotation, logout/local-state clearing, generic register/forgot responses, email
verification/reset and password-change session termination as implemented.

For a browser BFF verify real cookie attributes/CSRF, concurrent expired-token requests,
no token-bearing browser JSON/storage/logs/artifacts and no unsafe write retries. Use
real Identity in a disposable environment for contract/concurrency acceptance; fixtures
may test adapter failures but do not replace that evidence. Never demonstrate by
logging tokens or pasting real passwords into shell commands. Report unavailable
infrastructure and the commands actually executed; do not rerun the whole Identity
suite for a documentation-only change or label old results as freshly executed tests.
