# Отчёт по этапу 7

## 1. Этап

**7 — Поставка как самостоятельного мини-продукта**, завершён 2026-10-19.
AGENTS.md и Identity_Codex_Spec.md прочитаны целиком; проверены код и Git history.
Исходное дерево было чистым. Этапы 0–6 подтверждены commits d7c4f94, 8d901d7,
647ae4c, 4f64b57, 6e793da, 32fa225, 7e9fb07 и существующими тестами.
Выполнены требования этапа 7 §10 и отчёт §13. Подготовлена локальная поставка **1.0.0**;
Git tag, commit, registry publication и внешний deployment не выполнялись.

## 2. Реализовано

- Финальный multi-stage Dockerfile: официальные .NET 10 SDK/runtime manifests закреплены
  digest, publish output без SDK в runtime, non-root, OCI title/version/revision,
  liveness HEALTHCHECK. Dev PostgreSQL/Mailpit также pinned digest.
- Container probe `dotnet Identity.Api.dll --health-check` в отдельном процессе:
  до host/DI, только фиксированный loopback GET /health/live, timeout, без redirect/
  proxy, exit 0 только для 200 healthy, без секретов/ответа в stdout/stderr.
- Версия 1.0.0 в Directory.Build.props и OCI label, CHANGELOG с release notes,
  миграциями и явными ограничениями продукта.
- GitHub Actions CI: restore/tools/build/format, все unit/API/PostgreSQL/Mailpit tests
  с Up/Down/reapply migrations, model drift/idempotent SQL, container build/smoke,
  секреты current Git tree, TRX artifacts 7 дней. Actions закреплены SHA,
  contents:read, credentials не сохраняются; нет publish/deploy jobs.
- Secret guard для environment/key files и известных PEM/access-key/JWT literals;
  self-tests и локальная опция IncludeUntracked. Findings содержат filename/rule,
  не совпавшую строку/значение.
- README с последовательным dev quick start, сохранением существующих ключей/env,
  protected connection-string construction, отдельными миграциями и CI-командами.
- Полный curl onboarding register → Mailpit → verify → login → refresh → me → logout
  → failed refresh, прежние curl для всех операций и ручной JWKS validation пример сохранены.
- Production runbook и отдельные Compose/env samples: immutable image, TLS/proxy,
  SMTP TLS, secret mounts, runtime DB grants, отдельный migration runner, retention,
  backup/restore/security reconciliation, key rotation и monitoring.
- Stage7 smoke включает предыдущие сценарии, явные refresh→me и logout→failed refresh,
  проверку Docker health, non-root и version label. Временные ресурсы изолированы.

Ключевые файлы (относительно корня):

- `Dockerfile`, `Directory.Build.props`, `compose.yaml`.
- `src/Identity.Api/Configuration/ContainerHealthProbe.cs`, `src/Identity.Api/Program.cs`.
- `.github/workflows/ci.yml`, `scripts/Test-RepositorySecrets.ps1`,
  `scripts/Test-Stage7Smoke.ps1`, общий helper `scripts/Test-Stage3Smoke.ps1`.
- `deploy/compose.production.yaml`, `deploy/production.env.example`, `docs/production.md`.
- `README.md`, `CHANGELOG.md`, `docs/api.md`, `docs/architecture.md`,
  `docs/deployment.md`, `docs/security.md`.

## 3. Решения

Clean Architecture/Controllers/собственный CQRS сохранены. Business handlers, Domain,
Application, EF schema и NuGet dependencies не менялись. Новых миграций нет: выпуск
использует InitialIdentity, SessionAudienceBinding и SecurityMaintenanceIndex.
Health CLI — operational probe отдельного процесса, не новый business endpoint.
Он повторно использует runtime assembly, чтобы не добавлять curl/OS tools/SDK в image.
Docker health означает liveness; DB readiness, schema и SMTP проверяются отдельно.

Dev Compose остаётся Development/loopback/Mailpit. Production sample отдельный,
запускает только API и требует операторские PostgreSQL/SMTP/TLS/secrets. Для file mounts
ключам нужны права чтения non-root UID; read-only rootfs/tmpfs/cap_drop не подменяют
secret vault и сетевой контроль. Все operator inputs меняются конфигурацией, без C#.

CI выбран GitHub Actions на Ubuntu 24.04 с Docker и PowerShell 7.4+. Использованы
проверенные upstream releases [checkout](https://github.com/actions/checkout/releases/tag/v7.0.1),
[setup-dotnet](https://github.com/actions/setup-dotnet/releases/tag/v6.0.0),
[upload-artifact](https://github.com/actions/upload-artifact/releases/tag/v7.0.2),
закреплённые на полный commit SHA. Workflow не использует production credentials,
pull_request_target, image push или deployment. TRX публикуются только как test reports.

MCR digests получены через официальный registry (`docker buildx imagetools inspect`);
PostgreSQL/Mailpit digests проверены локальным image metadata и успешным build/smoke.
Обновление pinned images/actions требует review/rebuild/retest; pin не обещает
автоматическую установку будущих security patches.

Secret guard намеренно небольшой и без новых зависимостей: текущие tracked files,
локально также untracked. Это не full-history/все возможные форматы passwords scanner.
Секретные `.env`, keys, generated SQL, TestResults и logs исключены из Git.

## 4. Проверки

Выполнены из корня checkout, .NET SDK 10.0.402, PowerShell 7.6.5, Docker Linux:

```powershell
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
dotnet clean Identity.slnx --configuration Release
dotnet restore Identity.slnx
dotnet tool restore
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger "trx;LogFilePrefix=stage7-clean"
dotnet format Identity.slnx --no-restore --verify-no-changes
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
dotnet list Identity.slnx package --vulnerable --include-transitive
pwsh -NoProfile -File scripts/Test-Stage7Smoke.ps1
git diff --check
```

- Clean/restore/tools/Release build/format успешны, **0 warnings, 0 errors**.
- **339 passed, 0 failed, 0 skipped:** Unit 106, API 45, Integration 188. TRX stage7-clean
  в локальных TestResults тестовых проектов. Набор тестов этапа 6 сохранён; проверяются
  PostgreSQL migrations/constraints, concurrent registration/refresh/reset, replay,
  JWT negative cases, IDOR, rollback, production validation и log/metric secrecy.
- EF pending model changes нет, idempotent SQL сформирован; все три migrations
  Up/Down/reapply прошли на реальном PostgreSQL 17. Новая схема этапом 7 не создавалась.
- NuGet audit: у семи проектов уязвимые packages не обнаружены по текущим sources.
- Docker smoke exit 0: image собран с pinned bases, register→Mailpit→verify→login→
  refresh→me→logout→failed refresh, плюс restart/replay/sessions/password/deactivation,
  headers/error contract и SQL audit state. Docker HEALTHCHECK healthy, CLI probe exit 0,
  UID != 0, version label = 1.0.0. Временные containers/network/volume/env/keys удалены.
  Лог: `TestResults/stage7-docker-smoke.log` (ignored).
- Дополнительно: одноразовый container того же image с ASPNETCORE_HTTP_PORTS=invalid
  и --health-check завершился exit 1 без вывода конфигурации/response/exception.
- Guard self-tests проходят. Negative plumbing test создал временный untracked
  private-key sentinel: guard отклонил его, не отразив marker в выводе; файл удалён,
  повторный guard current tracked/untracked tree прошёл.
- README ConvertFrom-StringData проверен на `.env.example`: URL fragment `{token}`
  сохраняется. Smoke использует эквивалентный изолированный env/migration/bootstrap flow.
- Production Compose проверен `docker compose --env-file <temporary-env>
  -f deploy/compose.production.yaml config --quiet` с отдельным
  IDENTITY_RUNTIME_ENV_FILE и временными placeholder mounts; exit 0, без запуска
  production containers и без изменения пользовательского `.env.production`.
- Workflow reviewed; все его build/test/migration/smoke команды выполнены локально.
  Сам GitHub-hosted workflow **не запускался**. Попытка дополнительного YAML parse
  bundled Python не выполнилась из-за отсутствующего PyYAML; проверки Compose использовали
  реальный Docker Compose parser. Внешний production deploy/реальный TLS/SMTP не выполнялся.
- `git diff --check` и финальная проверка секретных paths проходят. Git предупреждает
  только о нормализации LF→CRLF; generated SQL/TRX/keys/logs не добавляются.

## 5. Безопасность

Поставка отделяет source/build/runtime/production config, закрепляет images/actions,
сохраняет non-root и не копирует secrets в Docker context/image. CI без production
секретов и write permissions, без автоматической публикации. Guard помогает отклонять
секретные файлы/literals до merge. Container probe не логирует credentials/response
и не вызывает key/SMTP/EF initialization.

Production инструкция фиксирует TLS trust boundaries, сетевое закрытие health/backend,
runtime DML vs migration DDL, сохранение RSA/AES при restart/restore, keys до разбора
backlog, explicit backup retention и риск восстановления старых credentials/revocations.
Существующие security controls этапов 0–6 и негативная test matrix проходят без изменений.

## 6. Ограничения/блокеры

Блокеров этапа 7 нет. Локальная поставка 1.0.0 подготовлена и проверена.
GitHub Actions требуется первый реальный run после принятия изменений; локальные
проверки не называются выполнением удалённого workflow. Production sample — шаблон
с placeholders: actual DNS/proxy IP, immutable image digest, keys, PostgreSQL/SMTP
и grants задаёт оператор. Registry upload, Git tag/release, CI run и deployment во
внешние аккаунты не выполнялись.

Остаются границы v1: без OAuth2/OIDC/SSO/MFA/social login/frontend; clientId публичный,
downstream JWT действует до exp (+skew); локальные rate limits; SMTP at-least-once;
best-effort telemetry; generic responses не constant-time; PII/credentials после
деактивации сохраняются. Guard не заменяет историю/review произвольных passwords.
Production TLS/SMTP/PITR и целевая нагрузка проверяются оператором отдельно.

Restore snapshot откатывает также credentials/status/revocation; runbook требует
security reconciliation и аннулирования восстановленных session/one-time tokens
до возвращения трафика. Автоматический force-password-reset/admin recovery не вводился
вне спецификации. Подробные ограничения — CHANGELOG и docs/security.md.
Противоречий требованиям этапа 7 не выявлено. Новая функциональность вне v1 не добавлялась.

## 7. Следующий этап

Этапы **0–7** выполнены; следующего numbered stage в ТЗ нет. Рекомендация — отдельная
финальная приёмка/review по матрице требований, затем операторская публикация и
production rehearsal с реальными инфраструктурными настройками. Не выполнялись
автоматически и требуют отдельного запроса.
