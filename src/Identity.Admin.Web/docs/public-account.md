# Public account pages — stage 11.13

`/account/activate#token=…` accepts an invitation; `/account/reset-password#token=…`
sets a new password using an emailed recovery link. These routes are outside
`AuthProvider`: opening/submitting either page does not request an admin session,
CSRF token or privileged data. Existing administrator cookies are not required.
The UI uses the existing PublicLayout, default Ant Design Card/Form/Input/Result
and responsive sign-in geometry. No backend, migrations or packages were changed.

## Link and password handling

Before React, routing or providers mount, `captureAccountLink` reads the fragment
and immediately removes it with `history.replaceState`, preserving the existing
history state/path/search. Only a single `token` parameter with the backend's
43–512 ASCII base64url character format is accepted. Query parameters are never
accepted as token sources. Email templates must continue to use fragments, not
queries; use the actual UI origin and route in deployment configuration.

Bootstrap hands the link to component memory and releases its temporary reference
after commit. This also supports React StrictMode's repeated initialization.
`watchAccountLinks` registers native popstate/hashchange observers before the
router. Opening another fragment on the same document clears the URL before
router observers run and hands the new link directly to the mounted form. No
token is kept in the observer after handoff. A new link resets a previous result,
clears password fields and aborts an outstanding request for the old link.
There is no persisted recovery: reload shows Link required. Open the complete
email link again if the page was closed or reloaded before completion.

Passwords are not trimmed or normalized before transport. The client validates
15–128 Unicode code points after NFKC normalization, valid Unicode and backend
UTF-8 input limits (1,024 bytes raw, 512 normalized). Spaces are supported;
arbitrary character-class requirements are absent. Confirmation must match the
entered password exactly. The backend remains authoritative, including its
common-password denylist, and applies NFKC consistently during hash/verify.

`features/account/api.ts` sends only `{ token, newPassword }` as JSON via the typed
central API client to existing generated OpenAPI paths:

- `POST /api/v1/Auth/invitations/accept`
- `POST /api/v1/Auth/password/reset`

ASP.NET routing is case-insensitive; generated casing is retained. Neither call
uses Query queries/mutations, storage, notifications, logging, tracking or URL
parameters for credentials. No new native-token login/refresh UI was added.
Passwords are cleared after each completed attempt. The link remains in component
memory for explicit retry after password-policy/network failures; success or a
terminal invalid-token response removes it. Unmount releases the component and
aborts pending transport. Requests have a 30-second timeout, a duplicate-submit
guard and no automatic retry. Cancellation cannot undo an already committed reset.

## States and sign-in destination

Missing and malformed links have dedicated states. Backend expired, consumed and
invalid tokens share one generic Link unavailable message. Weak/common password,
confirmation mismatch, rate limiting and uncertain network/server results have
safe feedback; raw ProblemDetails messages and token values are not rendered.

Success does not create a session or navigate automatically. Ordinary users sign
in through their consuming application. The existing `/admin/login` link is
explicitly labelled Administrator sign in; invitation acceptance does not grant
an administrative role. There is no arbitrary return URL or new authentication
flow. Reset explicitly warns that existing sessions are revoked.

## Verification

`account.test.tsx` covers synchronous hash removal/history preservation, malformed
and missing fragments, flow isolation, anonymous routing, StrictMode handoff,
JSON-only transport, empty Query caches/storage, Unicode/raw-size validation,
safe server errors, duplicate submission, cancellation and timeout.
Regression coverage also checks same-document fragment navigation after success
and verifies that later router observers see an empty hash.

`e2e/account.spec.ts` runs after the shared administrative fixture checks. With
`scripts/Test-AdminWebSmoke.ps1` it invites a unique account, reads the delivered
Mailpit link in Node memory, activates anonymously, requests a reset through the
real admin API, changes the password anonymously and verifies native login plus
old-refresh rejection. Reuse is rejected for both links. Missing/malformed/reload,
weak/common passwords, mismatch and network uncertainty are also covered.
Keyboard/layout screenshots at 360/768/1280 contain empty forms and no fragment.
Real tokens/passwords are not test titles, assertion values or attachments;
secret-bearing helper failures are sanitized and traces/video are disabled.

Full release UX/security acceptance and production routing/proxy hardening remain
separate stages 11.14/12. Do not add analytics before link cleanup; retain the
existing no-referrer policy. The repository's dev HTTPS proxy is for local use.
