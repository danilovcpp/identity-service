# Identity REST snapshot

Verified against Identity1.1.0, source commit a7dbb2f (2026-10-21). Confirm the actual
deployment/configuration before use. JSON is camelCase. No SDK/OAuth endpoints are implied.

## Public/user contract

Paths below are exact. A body marked absent is not an empty invented DTO. Bearer
means a valid access token in Authorization; user ownership comes from verified sub/sid.

| Method | Path | Access; request | Success |
| --- | --- | --- | --- |
| POST | `/api/v1/auth/register` | Public; `{email,password,displayName?,clientId}` | 202 generic message, no user ID/tokens |
| POST | `/api/v1/auth/email/verification-request` | Public; `{email}` | 202 generic |
| POST | `/api/v1/auth/email/verify` | Public; `{token}` | 204 |
| POST | `/api/v1/auth/login` | Public; `{email,password,clientId}` | 200 token pair |
| POST | `/api/v1/auth/refresh` | Public; `{refreshToken}` | 200 replacement pair |
| POST | `/api/v1/auth/logout` | Public; `{refreshToken}` | 204 idempotent |
| POST | `/api/v1/auth/logout-all` | Bearer; body absent | 204 |
| GET | `/api/v1/users/me` | Bearer; body absent | 200 profile |
| PATCH | `/api/v1/users/me` | Bearer; `{displayName}` only, nullable | 200 profile |
| POST | `/api/v1/users/me/deactivate` | Bearer; `{password}` | 204 |
| POST | `/api/v1/auth/password/change` | Bearer; `{currentPassword,newPassword}` | 204 |
| POST | `/api/v1/auth/password/forgot` | Public; `{email}` | 202 generic |
| POST | `/api/v1/auth/password/reset` | Public; `{token,newPassword}` | 204 |
| POST | `/api/v1/auth/invitations/accept` | Public; `{token,newPassword}` | 204 |
| GET | `/api/v1/sessions` | Bearer; body absent | 200 own active session array |
| DELETE | `/api/v1/sessions/{id}` | Bearer; body absent | 204 own session; foreign/absent404 |
| GET | `/.well-known/jwks.json` | Public; body absent | 200 public JWK Set |

Token pair: `{accessToken,refreshToken,tokenType:"Bearer",expiresIn}`. expiresIn is
access-token lifetime in seconds, not refresh/session expiry. No user ID, email,
roles, refresh expiry or session cookie is returned by ordinary login. Obtain identity
from a validated JWT and/or `/api/v1/users/me`.

Profile: `{id,email,displayName,emailVerified,status,createdAt}`. PATCH accepts only
displayName, max120 characters after trim; null clears it. Missing/blank/unknown fields,
including email/userId, are rejected. Email is immutable through current APIs.

Sessions: array of `{id,clientId,createdAt,lastUsedAt,absoluteExpiresAt,userAgent,isCurrent}`.
Only active own sessions appear; userId query cannot select another owner. This is
distinct from the administrative sessions listing DTO.

## Semantics the adapter must preserve

- Registration always gives generic202 for eligible-shaped requests including existing
  email; do not expect409 or infer account existence/delivery. Unverified users cannot
  log in. Verification resend and forgot are also generic. 202 means accepted processing,
  not delivered email. Never expose enumeration detail from an app-owned lookup.
- Login gives generic401 invalid_credentials for wrong/missing/inactive/locked user or
  unknown/disabled client. clientId is public audience selection, not client authentication.
  Login does not impose today's stronger signup password rules on existing passwords.
- Refresh accepts only the opaque refresh token; client/audience are bound to its
  session and cannot be switched. Unknown/expired/revoked/disabled/locked cases give
  generic401 invalid_refresh_token. Reusing consumed refresh revokes the whole family,
  including any concurrent successful replacement; no grace/retry of the old token.
- Logout accepts known consumed/expired tokens to revoke their family too. Unknown,
  malformed or already revoked tokens also give204. Logout does not mint a new pair.
  Logout-all applies across this identity's clients and also revokes admin sessions.
- Password change/reset/deactivate give204 and revoke all sessions, including current
  and administrator sessions. There is no auto-login/new pair afterwards. Deactivation
  is terminal in current public APIs and retains PII; it is not account erasure or
  merely leaving one startup. Design app-membership closure separately when shared.
- Reset token is single-use; invalid/expired/used/wrong-purpose cases are indistinguishable
 400 invalid_password_reset_token. New reset issuance supersedes older reset links.
  Invitation acceptance activates an Invited user without an existing password. Do not
  assign a password through admin API or infer credentials exist on every user.

Passwords: NFKC-normalized consistently by the service,15–128 Unicode code points,
max1024 UTF-8 bytes before normalization/512 after, local denylist. Preserve spaces;
never trim/truncate/silently change input or require artificial complexity classes.
Client hints/confirmation can match the service but backend validation is authoritative.

Default lifetimes (configurable): access900s, refresh up to7d, absolute session30d,
verification24h, reset30m, invitation72h. Use expiresIn/current configuration, not a
client hardcoded assumption. Auth/profile bodies are bounded to16KiB. Rate limiting
includes IP and account; handle429/Retry-After without unsafe automatic replay.

Errors are ProblemDetails with code, traceId and optional errors keyed by fields.
Use HTTP status + machine code and safe constant UI text; no raw body/exception echo.
Expect400 validation/token/CSRF,401 authentication,403 authorization,404 absent/foreign,
409 nonsensitive conflict,429 limit,503 disabled/unavailable and generic5xx. Edge failures
can be non-JSON. An upstream timeout is not proof that a state-changing operation failed.

## JWT and public pages

Access token: RS256/kid; claims sub,iss,aud,jti,sid,client_id,iat,nbf,exp,token_use=access.
No email/business roles are promised. JWKS publishes only public RSA components,
with cache guidance300s. Issuer and network API base URL need not be identical in dev.

Identity Console supplies `/account/activate` and `/account/reset-password`, reachable
anonymously subject to the deployment's network topology. Token links use `#token=...`;
read once and immediately history.replaceState away the fragment before other effects,
keep token only in memory, submit JSON to the corresponding API, then discard it.
Do not put link tokens into query strings, navigation state, caches or analytics.

There is **no supplied `/account/verify` page**. For a consuming-app verification page,
use the existing JSON `/auth/email/verify` contract and an operator-configured fixed
Email:VerificationUrlTemplate pointing to that real page. Do not invent a service page
or accept arbitrary client redirect URLs. Current templates are service-wide; a shared
instance does not choose different startup URLs from request clientId. Coordinate
branding/return navigation explicitly; no implicit OAuth state/redirect protocol exists.
