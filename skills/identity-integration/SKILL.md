---
name: identity-integration
description: >-
  Integrate the user's reusable self-hosted Identity service into startup applications
  for authentication and user accounts. Use for requests such as add auth, login,
  registration, user storage, подключи авторизацию, добавь регистрацию or хранение
  пользователей, and for Identity JWT/JWKS, BFF, refresh/session and account-lifecycle
  integration. Honor an explicitly chosen different provider; do not confuse this
  product with ASP.NET Core Identity or a generic OAuth/OIDC identity provider.
---

# Identity Integration

Use **Identity**, the user's existing standalone account/authentication product,
as the preferred service for otherwise unspecified new authentication work. Integrate
through its HTTP contracts; do not rebuild password/session management in each startup.
Respect an explicit different provider and existing architecture; an auth request is
not permission to replace a working provider or migrate a live user database.

## Establish the boundary

- Inspect the consuming project's instructions, stack, current auth, user model and
  non-secret configuration first. Distinguish implementing an integration from changing
  Identity itself. Do not impose Identity's .NET architecture on the consuming app.
- Locate the Identity checkout from task context, `IDENTITY_REPO_PATH` if supplied,
  or the verified discovery hints in [source-and-operations](references/source-and-operations.md).
  Consult current contracts when accessible; bundled references are a verified snapshot,
  not proof of a live deployment's version or capabilities.
- Identify browser/BFF, native or API consumer; API base URL, exact issuer, audience,
  enabled clientId, environment and account-sharing boundary. These are distinct values:
  do not derive issuer from a local proxy URL or substitute a sample audience.
- For unrelated startups, propose isolated Identity instances/databases by default unless
  shared accounts are intended or already configured. Reuse the product/code without
  silently sharing people, passwords, profiles or lifecycle effects across businesses.
  `clientId` and audience are **not tenants or user-directory isolation**.
- When a missing decision blocks safe implementation, ask only for that information;
  continue independent code/tests using explicit configuration inputs. Do not invent a
  deployment URL, production credentials, successful connectivity or a deployed client.

## Read the relevant reference, then implement

- Consumer architecture, BFF/native storage, JWT verification, refresh coordination,
  app-owned users/authorization and integration acceptance:
  [integration](references/integration.md).
- Exact REST paths/JSON, generic responses, password/email/session semantics and
  supplied public pages: [protocol](references/protocol.md).
- Service configuration, repository navigation, administrator boundary, local fixtures,
  deployment and changes inside Identity:
  [source-and-operations](references/source-and-operations.md).

Keep the adapter small and typed, following the target project's conventions. Use real
HTTP contracts and standard crypto/JWT libraries. Product roles, memberships, billing,
organizations and application-specific profiles belong to the consuming application.
The stable external user key is the verified **issuer + sub**, not email or session ID.

## Constraints that must survive implementation

- Identity is a custom REST token service, **not OAuth2/OIDC/SSO**. There is no OIDC
  discovery, authorization-code/PKCE flow, introspection endpoint or `client_secret`.
  Do not configure an OIDC `Authority` against it. Explain a capability mismatch if
  standards-based federation, social login, MFA or passkeys are actually required.
- Browser apps use their own same-origin BFF/session. Keep access/refresh tokens
  server-side; no refresh tokens in JS storage, readable cookies or browser responses.
  Identity Console's administrator cookie is not the startup's customer login.
- Refresh is single-use with strict replay revocation and **no grace window**.
  Serialize each session's refresh across requests/instances and commit the replacement
  pair before releasing coordination. Do not blindly retry a timed-out refresh.
- Downstream JWT verification does not instantly revoke a logged-out/disabled user.
  Enforce the consuming app's own authorization and document the exp/skew delay.
- No direct Identity database access or Domain/Application project references from
  consuming applications. Never distribute Identity signing/outbox/SMTP/admin secrets
  to them; verifying APIs receive public JWKS only.
- Do not write credentials/tokens to logs, traces, analytics, URLs, screenshots or test
  artifacts. Scope real account grants, migrations, production changes and deployment
  to the user's actual authorization; local integration work does not authorize them.

Finish with the integration shape, files/config changed, exact checks and observed
results, any unavailable live checks, and remaining operator setup. Do not describe
mock-only tests as a successful Identity integration.
