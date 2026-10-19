# Changelog

## Unreleased — v1.1, stages 8–10, 11.1–11.9

- Administrator Disable/Enable confirmations in user details, required disable reason,
  state/role gating and explicit session/link revocation consequences.
- Existing CSRF-protected commands with204-only feedback, last-administrator/state
  conflicts, no optimistic status or automatic replay; refreshed account reads.
- Real-backend verification of blocked sign-in, revoked admin cookie and restored
  Invited state, with responsive keyboard confirmation checks.

- Administrator invitation form and confirmed resend for Invited accounts, using
  existing cookie/CSRF API, typed mutations and targeted query invalidation.
- Validation, conflict/uncertain-network feedback, double-submit protection and202
  processing acknowledgment; no password assignment or guaranteed-delivery claim.
- ReadOnly controls absent; disposable HTTPS/PostgreSQL/Mailpit checks include real
  invite/resend mail receipts and responsive keyboard/focus behavior.

- Read-only account details with status, verification and lifecycle timestamps,
  accessible ID copy feedback and explicit invalid-ID/not-found/error states.
- Latest ten retained account security events via existing server-side audit filter;
  independent loading/error/retry and cache projection without metadata or secrets.
- Catalog filters remain in detail URLs across reload/login and Back to users.

- Read-only users catalog with server pagination/sort/filter/search, 400ms debounce,
  validated canonical URL state and browser history restoration for both admin roles.
- Responsive Ant Design table, local timestamps with UTC tooltips, explicit empty/error/
  retry states and page-size control on mobile; safe links to the future details route.
- Typed existing API integration with cancellation/cache isolation, no backend changes.

- Live administrative dashboard with seven server counters, snapshot time and UTC
  30-day registration chart, keyboard tooltip and accessible daily-value table.
- ReadOnly/Administrator access, explicit loading/empty/error/GET refresh states,
  safe numeric normalization and isolated dashboard loading with Recharts3.10.1.
- Real-backend browser verification of refreshed counts after public registration.

- Administrative login/logout, cookie-backed session checks, protected routing and
  explicit Forbidden state, server profile/role and session cache cleanup.
- Password-free Query cache, CSRF-protected commands and safe internal return paths.
- Disposable HTTPS browser smoke using PostgreSQL/Mailpit, REST verification and CLI
  bootstrap for both administrative roles; no backend contract or policy changes.

- Identity Console light theme, responsive sidebar/mobile drawer, public/admin layout
  branches and NotFound, shared loading/empty/error/confirmation/status components.
- Keyboard/focus recovery, render error boundary, RTL and production Chromium shell
  checks. Future sections remain disabled; no authentication or business screens added.

- Generated OpenAPI types, typed same-origin Fetch client with memory-only CSRF,
  cancellation, normalized errors and no automatic command retries.
- Isolated TanStack Query providers/key factories and transport/cache tests.
  Successful user details/session OpenAPI metadata corrected with explicit approval;
  existing HTTP contracts and business behavior preserved.

- Identity Console bootstrap: React 19, strict TypeScript, Vite, Ant Design 6,
  pinned npm dependencies, typed linting, formatting and Vitest/RTL smoke test.
- Server-only development proxy settings, optional local HTTPS and frontend runbook.
  API integration, authentication and administrative screens remain separate stages.

- Administrative invite/resend/reset/disable/enable/session revocation, protected by
  cookie/CSRF and transactional actor revalidation. Last-administrator race protection.
- Email invitation acceptance with recipient-chosen password, hash-only single-use
  token and encrypted transactional outbox; additive AdminWriteLifecycle migration.
- Exact prior-status restoration on enable; disabling revokes both session transports
  and one-time links. Invitation settings/fragment-only templates and PostgreSQL/Mailpit tests.

- Read-only admin dashboard/users/details/sessions/audit API for both system roles,
  server pagination, fixed sort whitelist, bounded literal ILIKE search and UTC filters.
- DTO-only SQL projections, repeatable-read snapshots, explicit30-day UTC trend window,
  AdminReadIndexes migration and PostgreSQL/query-count tests.

- Identity system administrator/read-only roles, protected local grant/revoke CLI.
- Separate server-side admin sessions, Secure HttpOnly cookies, CSRF and JSON auth API.
- Durable Data Protection, per-request validation, idle/absolute expiry, audit actor/target.
- Atomic last-administrator protection including existing self-deactivation; password
  change/reset/logout-all revoke admin sessions alongside existing refresh sessions.
- Additive AdminSecureAccess migration, Invited/Invitation schema preparation. Existing
  Bearer REST protocol preserved; admin frontend remains stage11.

## 1.0.0 — 2026-10-19

Подготовленный первый самостоятельный выпуск Identity, .NET 10 / PostgreSQL 17+.
Git tag, registry publication и deployment выполняются оператором отдельно.

### Возможности

- Email/password registration, verification/resend, generic responses и login lockout.
- RS256 access JWT/JWKS с audience binding; непрозрачные refresh tokens, rotation,
  строгий replay detection, logout/logout-all и управление собственными сессиями.
- Собственный профиль, immutable email, displayName, смена/одноразовый сброс пароля,
  терминальная деактивация с отзывом всех сессий.
- SMTP transactional outbox: AES-256-GCM whole-message encryption, retries/backoff,
  безопасный claim несколькими workers и очистка payload после успешной отправки.
- Транзакционный security audit, correlation, безопасные logs/metrics, ограниченный
  retention audit/processed outbox/one-time tokens и refresh history.
- Clean Architecture, Controllers, собственный типизированный CQRS без MediatR.

### Поставка

- Non-root Docker image с pinned base digests, liveness healthcheck без curl/SDK,
  OCI version 1.0.0; локальный Compose с PostgreSQL/Mailpit.
- Отдельные production Compose/env samples и руководство TLS/proxy, SMTP, keys,
  миграций, backups/restore, обновлений и мониторинга.
- CI restore/build/format/test, PostgreSQL migrations, container smoke и guard
  против секретных файлов/известных token/private-key literals в текущем Git tree.
- Полный end-to-end smoke register → Mailpit → verify → login → refresh → me →
  logout → failed refresh, плюс сценарии предыдущих этапов.

### Миграции

Выпуск включает существующие `InitialIdentity`, `SessionAudienceBinding`,
`SecurityMaintenanceIndex`. В этапе 7 новая схема не вводится. Миграции применяются
отдельно до обновления API; auto-migrate отсутствует. Legacy sessions без audience
binding требуют нового login. Обновляя установленную версию, сохраняйте JWT/AES keys.

### Ограничения

- Собственный REST protocol; OAuth2/OIDC/SSO, MFA/social login/passkeys и frontend
  отсутствуют. Browser integration требует внешнего BFF и защищённого server storage.
- clientId публичный, не подтверждает подлинность приложения; business roles/tenants нет.
- JWT в downstream действует до exp (+skew); Identity проверяет отзыв через БД.
- IP/account limits локальны процессу; SMTP at-least-once; telemetry best-effort.
- Generic errors не гарантируют одинаковую latency. Password denylist локальный,
  без полного каталога утечек. Deactivation сохраняет PII/credential, не стирает данные.
- Reset/verify pages, TLS terminator, production SMTP/PostgreSQL и metrics collector
  предоставляет оператор. Production template требует заполнения; dev Compose — loopback HTTP.
- Secret guard проверяет текущий tree и известные форматы, не всю историю и не все
  произвольные пароли. Backups имеют отдельную retention/restore security policy.

Руководство: [README](README.md), [production](docs/production.md),
[API](docs/api.md), [security checklist](docs/security.md).
