# Identity 1.0.0

Развитие v1.1: **этап 8** добавляет системные административные роли, CLI bootstrap и
secure cookie/CSRF API. [Настройка и runbook](docs/admin-access.md). Admin access выключен
по умолчанию; HTTPS и постоянный защищённый Data Protection directory обязательны.
Существующие REST Bearer API сохраняются. Admin frontend и dashboard пока не реализованы.

Самостоятельный self-hosted backend учётных записей и аутентификации на .NET 10 и
PostgreSQL 17+. Реализованы этапы 0–7 [технического задания](Identity_Codex_Spec.md):
registration/verification, RS256 JWT/JWKS, refresh rotation/replay, собственные sessions/
profile, password lifecycle, deactivation, encrypted SMTP outbox и security audit.
Состав выпуска и ограничения — в [CHANGELOG](CHANGELOG.md).

## Требования

- .NET SDK 10.0 stable (`global.json`, rollForward latestFeature).
- Docker Engine с **Linux containers**, Compose v2 и доступным daemon.
- PowerShell **7.4+** (`pwsh`) для команд ниже и smoke scripts, Git.
  PowerShell доступен и для Linux/macOS. Тесты используют реальные PostgreSQL/Mailpit
  в Testcontainers; Docker обязателен, integration tests без него не пропускаются.

## Быстрый локальный запуск

Все команды выполняются из корня checkout в PowerShell 7.4+. Это dev окружение:
loopback HTTP, Development и Mailpit. Production описан [отдельно](docs/production.md).

1. Восстановите инструменты и соберите API:

```powershell
dotnet restore Identity.slnx
dotnet tool restore
dotnet build Identity.slnx --configuration Release --no-restore
```

2. Создайте локальные настройки и постоянные **случайные** dev keys. При повторном
запуске существующие `.env` и keys сохраняются; не добавляйте их в Git:

```powershell
if (-not (Test-Path .env)) {
    Copy-Item .env.example .env
    $identityPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    (Get-Content .env) -replace '^POSTGRES_PASSWORD=.*$', "POSTGRES_PASSWORD=$identityPassword" | Set-Content .env
    Remove-Variable identityPassword
}
New-Item -ItemType Directory -Path secrets -Force | Out-Null
if (-not (Test-Path secrets/outbox-dev.key)) {
    [System.IO.File]::WriteAllBytes((Join-Path (Get-Location) 'secrets/outbox-dev.key'), [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}
if (-not (Test-Path secrets/jwt-dev.pem)) {
    $identityRsa = [System.Security.Cryptography.RSA]::Create(3072)
    try { [System.IO.File]::WriteAllText((Join-Path (Get-Location) 'secrets/jwt-dev.pem'), $identityRsa.ExportPkcs8PrivateKeyPem()) }
    finally { $identityRsa.Dispose(); Remove-Variable identityRsa }
}
```

В существующем `.env` из ранних этапов проверьте непустой POSTGRES_PASSWORD и оба
URL templates из `.env.example`, включая RESET_PASSWORD_URL_TEMPLATE. Порты меняются
там же. Пример предназначен для простой unquoted key=value формы `.env.example`.
Не меняйте пароль уже инициализированного PostgreSQL volume только правкой `.env`.

3. Поднимите зависимости и примените **все три миграции** отдельной SDK-командой.
Connection string формируется в памяти из dev `.env` и не печатается:

```powershell
docker compose config --quiet
docker compose up --detach --wait postgres mailpit
$identityLocal = Get-Content .env -Raw | ConvertFrom-StringData
$identityConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
$identityConnection['Host'] = '127.0.0.1'
$identityConnection['Port'] = $identityLocal.POSTGRES_PORT
$identityConnection['Database'] = 'identity'
$identityConnection['Username'] = 'identity'
$identityConnection['Password'] = $identityLocal.POSTGRES_PASSWORD
$identityPreviousConnection = $env:Database__ConnectionString
try {
    $env:Database__ConnectionString = $identityConnection.ConnectionString
    dotnet ef database update --project src/Identity.Infrastructure --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Migration failed.' }
} finally {
    $env:Database__ConnectionString = $identityPreviousConnection
    Remove-Variable identityLocal, identityConnection, identityPreviousConnection
}
docker compose up --build --detach --wait api
docker compose ps
```

Если меняли шаблон/формат `.env`, задайте Database__ConnectionString через защищённый
ввод по [SDK-инструкции](docs/deployment.md#миграции). API не вызывает auto-migrate.
Значение `ready` означает DB connectivity, не актуальность schema или исправность SMTP.

4. Проверьте окружение:

```powershell
Invoke-RestMethod http://localhost:5080/health/live
Invoke-RestMethod http://localhost:5080/health/ready
Invoke-RestMethod http://localhost:5080/.well-known/jwks.json
```

| Сервис | Адрес по умолчанию |
| --- | --- |
| API / OpenAPI (только Development) | `http://localhost:5080` / `/openapi/v1.json` |
| Mailpit UI / SMTP | `http://localhost:8025` / `localhost:1025` |
| PostgreSQL | `localhost:54320`, database/user `identity` |

Все опубликованные dev порты привязаны к 127.0.0.1. API non-root; Docker healthcheck
использует `dotnet Identity.Api.dll --health-check` (liveness, без загрузки DB/keys).
HTTP port внутри образа — 8080. Mailpit worker очищает encrypted payload после success.
Обычный `docker compose down` сохраняет volume; добавление `--volumes` **удаляет БД**.

Далее пройдите [curl-сценарий API](docs/api.md#первый-end-to-end-сценарий) или запустите
полностью изолированный smoke:

```powershell
pwsh -NoProfile -File scripts/Test-Stage7Smoke.ps1
```

Smoke создаёт свои временные ports/env/keys/Compose project, применяет миграции,
собирает image и проверяет register → Mailpit → verify → login → refresh → me →
logout → failed refresh плюс replay/password/deactivation/restart. После себя удаляет
только свои containers/network/volume/env/keys. Для него пользовательский `.env` не нужен.
Страницы localhost:3000/verify и /reset из email templates не входят в backend:
локально скопируйте token из fragment ссылки Mailpit в JSON POST verify/reset либо
настройте внешнюю UI/BFF. Не передавайте одноразовые tokens в query API.

## Архитектура и API

```text
Domain          -> нет зависимостей
Application     -> Domain
Infrastructure  -> Application, Domain
Api             -> Application, Infrastructure
```

Controllers, собственные typed CQRS dispatchers/validators/handlers, явная DI и UoW.
Domain/Application не зависят от ASP.NET/EF. Нет MediatR, generic repository,
Identity user management или готового auth server. Schema: users, credentials,
sessions, refresh_tokens, one_time_tokens, outbox_messages, security_audit_events.

- [Архитектура и конкурентность](docs/architecture.md).
- [Все API contracts и curl](docs/api.md), включая пример JWT validation в отдельном .NET API.
- [Security checklist и ограничения](docs/security.md).
- [Options, SDK, миграции, key rotation, retention/metrics](docs/deployment.md).
- [Production runbook: TLS, secrets, PostgreSQL, deploy/backup/restore](docs/production.md).

## Сборка, тесты и CI

```powershell
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
dotnet clean Identity.slnx --configuration Release
dotnet restore Identity.slnx
dotnet tool restore
dotnet build Identity.slnx --configuration Release --no-restore
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet test Identity.slnx --configuration Release --no-build --no-restore
pwsh -NoProfile -File scripts/Test-Stage7Smoke.ps1
```

Tests применяют/revert/reapply migrations на временном PostgreSQL 17; EF InMemory/SQLite
не используются. Время и криптографические ключи тестов изолированы. DB из dev Compose
и connection string для `dotnet test` не нужны. Для проверки model snapshot и SQL
используйте команды [миграций](docs/deployment.md#миграции); SQL output ignored.

[GitHub Actions CI](.github/workflows/ci.yml) запускает те же проверки на Ubuntu/Docker,
включая migrations и container smoke. Actions pinned по SHA, permissions contents:read,
checkout без сохранения credentials; workflow не публикует image/release и не deploy.
Guard проверяет текущие tracked files (локально также untracked с флагом), запрещает
secret file paths и известные PEM/token literals; не заменяет проверку всей Git history
и ручной review произвольных passwords/config. TRX retention в CI — 7 дней.

## Границы v1.0

Собственный REST protocol **не OAuth2/OIDC/SSO**; MFA/social login/passkeys и frontend
не поставляются. clientId — публичный audience selector. Browser integration требует BFF:
refresh нельзя хранить в localStorage/sessionStorage/JavaScript-accessible cookie.
JWT в downstream действует до exp (+skew), IP/account limits локальны процессу,
SMTP at-least-once, deactivation сохраняет PII. Полный список — [release notes](CHANGELOG.md).
