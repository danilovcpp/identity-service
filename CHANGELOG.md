# Changelog

## Unreleased — v1.1, stage 8

- Identity system administrator/read-only roles, protected local grant/revoke CLI.
- Separate server-side admin sessions, Secure HttpOnly cookies, CSRF and JSON auth API.
- Durable Data Protection, per-request validation, idle/absolute expiry, audit actor/target.
- Atomic last-administrator protection including existing self-deactivation; password
  change/reset/logout-all revoke admin sessions alongside existing refresh sessions.
- Additive AdminSecureAccess migration, Invited/Invitation schema preparation. Existing
  Bearer REST protocol preserved; no admin UI/user-management endpoints yet.

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
