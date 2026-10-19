# Отчёт по этапу 6

## 1. Этап

**6 — Security hardening и observability**, завершён 2026-10-19.
Прочитаны AGENTS.md и Identity_Codex_Spec.md целиком; проверены код и история Git.
Исходное дерево было чистым. Этапы 0–5 подтверждены последовательностью commits
d7c4f94 → 8d901d7 → 647ae4c → 4f64b57 → 6e793da → 32fa225 и существующими тестами.
Требования: §5.7, §6.5, §7–9, этап 6 §10, проверки §11, отчёт §13.
Этап 7 не выполнялся.

## 2. Реализовано

- Единый JSON ProblemDetails writer до MVC writers, включая unsupported Accept и
  403. Model binding возвращает постоянный errors.request без raw value/JSON path.
  Сохранены прежние business error codes, validators и HTTP contracts.
- Correlation: один scoped OperationContext для audit, traceId, X-Correlation-ID,
  безопасного logging scope. W3C IDs, входной X-Correlation-ID игнорируется.
  Security/correlation headers восстанавливаются после ExceptionHandler.Clear.
- Production HTTPS guard для business API, HSTS, nosniff/no-referrer/no-store;
  HTTP health endpoints оставлены внутренним probes. Forwarded headers выключены
  по умолчанию; включаются только с exact trusted proxy IP, bounded ForwardLimit,
  симметричными X-Forwarded-For/Proto. X-Forwarded-Host игнорируется.
- Startup validation: unsafe trust-all switch, empty/wildcard production AllowedHosts,
  пустая/некорректная proxy allowlist и maintenance limits отклоняются. Существующие
  JWT/SMTP/AES/DB checks сохранены, включая запрет test-/dev- key IDs в production.
- Безопасные HTTP completion и committed security event logs. Framework URL/auth/
  binding/EF/Npgsql diagnostics подавлены; request Activity очищается от raw URL/query/
  user-agent tags перед завершением. Unknown clientId не записывается в login_failed audit.
- Meters Identity.Auth и Identity.Http: committed audit counters, active DB sessions
  gauge + sampled_at, HTTP requests/duration по route template/status. User/client/
  trace IDs, email/IP/token не labels. Прежние Identity.Email backlog/delivery metrics сохранены.
- SecurityMaintenanceWorker: ограниченные SKIP LOCKED batches для audit (90 дней),
  expired one-time tokens (expiry + 1 день), processed outbox (success + 7 дней).
  Pending/dead-letter, пользовательские данные и refresh replay evidence не удаляются.
  Добавлена третья миграция с partial outbox processed_at index.
- Дополнены security checklist, threat boundaries, операторские настройки, API/error
  contract, env samples и Docker smoke. Выполнены минимальные локальные login/refresh measurements.

Ключевые файлы относительно корня репозитория:

- `src/Identity.Api/Configuration/TransportSecurity.cs`, `Middleware/RequestSecurityMiddleware.cs`,
  `Middleware/JsonProblemDetailsWriter.cs`, `Middleware/RequestSizeLimitMiddleware.cs`,
  `Program.cs`, `appsettings.json`.
- `src/Identity.Application/Features/Auth/Login/LoginHandler.cs` (safe audit clientId).
- `src/Identity.Infrastructure/Observability/AuthMetrics.cs`, `Persistence/UnitOfWork.cs`,
  `Maintenance/SecurityMaintenanceOptions.cs`, `SecurityMaintenanceProcessor.cs`,
  `SecurityMaintenanceWorker.cs`, DI, outbox configuration/snapshot и migration
  `20261019012922_SecurityMaintenanceIndex`.
- `tests/Identity.ApiTests/SecurityBoundaryTests.cs`,
  `tests/Identity.IntegrationTests/SecurityObservabilityTests.cs`, `SecurityMaintenanceTests.cs`,
  `tests/Identity.UnitTests/SecurityConfigurationTests.cs`; прежние bootstrap/HTTP/migration tests
  адаптированы к HTTPS и трём миграциям, test workers выключены там, где они не предмет проверки.
- `.env.example`, `compose.yaml`, `scripts/Test-Stage6Smoke.ps1`, общий smoke helper,
  README и четыре документа в `docs/`.

## 3. Решения

Clean Architecture и явный CQRS/DI сохранены. Domain и его инварианты не менялись;
Application не получил ASP.NET/EF/Meter/ILogger зависимостей, новых NuGet packages нет.
HTTP security остаётся в API, telemetry/maintenance adapters — в Infrastructure.

HTTP в production отклоняется 400 https_required, без redirect тела credentials.
TLS terminator обязан сам отклонять публичный HTTP и закрывать доступ к backend/
health. Trusted forwarding использует штатный middleware по
[официальной документации Microsoft](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
В этой реализации поддержаны exact IP (до 16), не CIDR/trust-all, ForwardLimit 1–5.
HSTS 365 дней без preload/includeSubDomains; CORS остаётся выключенным.

Формат ошибок сохраняется даже при Accept HTML/XML: custom writer зарегистрирован
перед MVC, иначе один из MVC writers мог выбрать запрос и не записать 403 body.
Model binding намеренно не возвращает исходную диагностическую строку; подробные
полевые сообщения остаются только у типизированных CQRS validators.

Audit — источник истины. UoW собирает новые audit events после SaveChanges и публикует
metrics/logs только после успешного explicit CommitAsync; rollback/dispose очищает pending.
Implicit SaveChanges публикует после EF commit. Telemetry best-effort: exporter/logger
ошибка не превращает committed login в failure; crash после commit может потерять measurement.
HTTP instrumentation также не влияет на результат операции.

Retention §8/§5.7 завершён в рамках security/observability: три независимых batches
одной транзакцией, каждый <=BatchSize. Strict cutoff, SKIP LOCKED, rollback проверены.
Refresh cleanup и его безопасные сроки не изменены. Новая миграция создаёт только
индекс, без удаления/преобразования данных; Up/Down/reapply проверены.

Gauges показывают sampled DB lifecycle, не текущую пригодность JWT в каждом consumer;
session count не учитывает client config и temporary lockout. sampled_at позволяет
обнаружить устаревание. Counts глобальны для БД, их нельзя суммировать между replicas.
Exporter/collector и новый публичный metrics endpoint не добавлялись.

## 4. Проверки

Выполнены из корня репозитория с .NET SDK 10.0.402 и Docker Linux containers:

```powershell
dotnet restore Identity.slnx
dotnet tool restore
dotnet format Identity.slnx --no-restore
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger "trx;LogFilePrefix=stage6-full"
# Design-time connection: рабочая БД не используется.
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations add SecurityMaintenanceIndex --project src/Identity.Infrastructure --configuration Release
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
dotnet list Identity.slnx package --vulnerable --include-transitive
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-Stage6Smoke.ps1
git diff --check
```

- Restore, Release build и format успешны: **0 warnings, 0 errors**.
- Полная suite: **339 passed, 0 failed, 0 skipped** — Unit 106, API 45, Integration 188.
  +44 случая к 295 тестам этапа 5 (9 unit, 23 API, 12 integration). TRX stage6-full
  сохранены в локальных TestResults тестовых проектов (ignored).
- Новые целевые прогоны API/security suite выполнены до полного; обнаруженные
  failures устранены: порядок writers для 403, восстановление headers при исключении,
  runtime startup validation с фактической конфигурацией host.
- PostgreSQL 17/Mailpit/Testcontainers: прежние registration/refresh races, replay,
  JWT negative cases, logout/IDOR/reset/deactivation проходят. Новые тесты проверяют
  proxy spoofing, HTTPS/HSTS, CORS denial, generic model errors/unsupported Accept,
  chunked oversized input, crypto path invocation, correlation/audit/log secrecy,
  metrics после commit/rollback и отказа listener, retention boundaries/concurrency/
  locked rows/rollback. InMemory/SQLite не использовались.
- EF model drift отсутствует, idempotent migration.sql сформирован; три миграции
  полностью откатились/применились в test DB. Новая миграция применена также Docker smoke.
- NuGet audit: у всех семи проектов уязвимые packages не обнаружены по текущим sources.
- Docker smoke **exit 0**: build/publish non-root image, health, register → Mailpit →
  verify → login/JWKS → rotation/restart/replay → sessions/revoke/logout-all/logout →
  profile/change/reset/new login/deactivate; затем generic error/correlation/security
  headers и новый index. Временные containers/network/volume/env/keys удалены.
  Лог: `TestResults/stage6-docker-smoke.log` (ignored).
- Минимальные локальные measurements из полного прогона: 5 последовательных samples,
  PBKDF2=600000, реальный PostgreSQL. Login median/max **264.7/531.9 ms**, refresh
  **8.3/13.9 ms**. Это измерение на машине тестирования, без утверждения production SLA.
- `git diff --check` успешен; новые source/test files проверены. Секреты/ключи/.env,
  SQL output, TRX и Docker logs не добавляются в Git.

## 5. Безопасность

Закрыты выявленные gaps в HTTPS/proxy trust, подмене IP для limiter, отражении binding
ввода, неполном error contract, корреляции ошибок и audit, ложных метриках rollback,
PII/cardinality labels и неограниченном retention audit/token/processed outbox.
Существующие crypto/transactions/replay/owner isolation не ослаблены. Негативная
матрица и конкретные тесты перечислены в `docs/security.md`.

Production с test-/dev- keys или insecure SMTP/unsafe transport options не стартует.
Логи содержат только event type/trace или route template/status/duration, исключения —
только тип без object/message. Raw body/token/email/headers не логируются приложением.
Trace ID не служит авторизацией; отсутствие постоянного времени ответа не скрывается.

## 6. Ограничения/блокеры

Блокеров этапа 6 нет; применимые build/test/EF/Docker проверки выполнены.
Настоящий production TLS terminator/certificates/SMTP и saturation/load benchmark
на целевом VPS не запускались. HTTPS/proxy проверены в TestServer, Docker smoke —
локальная dev topology с Mailpit. Collector/exporter и deployment во внешние аккаунты
не настраивались. Оператор контролирует proxy logs, backups и retention за пределами БД.

Остаются задокументированные границы v1: без OAuth2/OIDC/SSO/MFA; public clientId;
downstream JWT revocation lag до exp (+skew); per-process rate limits; SMTP at-least-once;
PII/credential после деактивации сохраняются. Generic responses и общий verifier path
не гарантируют одинаковую latency. Pending/failed outbox хранится для ручного разбора;
AES keys нельзя удалять до разбора backlog. Audit backups требуют отдельного удаления.

При обновлении нужны третья миграция, explicit AllowedHosts и trusted proxy settings
для production TLS termination. Dev Compose сохраняет прежние адреса/режим.
CI workflow, release notes 1.0.0 и финальная продуктовая поставка не добавлены.
Противоречий обязательным требованиям этапа 6 не выявлено. Commit/deploy не выполнялись.

## 7. Следующий этап

Рекомендация: **7 — Поставка как самостоятельного мини-продукта** по ТЗ.
Не начат; выполнение только по отдельному запросу пользователя.
