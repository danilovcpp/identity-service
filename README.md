# Identity

Самостоятельный self-hosted сервис учётных записей и аутентификации на .NET 10.
Источник требований — [Identity_Codex_Spec.md](Identity_Codex_Spec.md).

Реализованы **этапы 0–3**: каркас, Domain, EF Core/CQRS, регистрация и подтверждение
email через зашифрованный transactional outbox и SMTP, login с RS256 JWT/JWKS,
сессией и начальным refresh token. Refresh rotation/logout/sessions API и профиль
относятся к следующим этапам.

## Требования

- .NET SDK 10.0 (stable; `global.json` допускает новые feature bands 10.0).
- Docker Engine с Linux containers и Docker Compose v2 для локального окружения
  и интеграционных тестов (Testcontainers запускает PostgreSQL 17 и Mailpit).

## Сборка и тесты

```sh
dotnet restore Identity.slnx
dotnet tool restore
dotnet build Identity.slnx --no-restore --configuration Release
dotnet test Identity.slnx --no-build --no-restore --configuration Release
```

Тесты запускают временные PostgreSQL/Mailpit, создают временный ключ, применяют миграцию и удаляют ресурсы после
проверок. БД из Compose и ConnectionString для `dotnet test` не требуются.
Без доступного Docker integration tests завершаются ошибкой, а не пропускаются.

## Запуск API с SDK

Сначала запустите PostgreSQL из Compose (настройка `.env` описана ниже):

```sh
docker compose up --detach --wait postgres
```

Для API и EF CLI обязательна `Database__ConnectionString`. В PowerShell 7 задайте её
через скрытый ввод, чтобы значение не попало в историю команд:

```powershell
$env:Database__ConnectionString = Read-Host 'PostgreSQL connection string' -MaskInput
dotnet ef database update --project src/Identity.Infrastructure --configuration Release
dotnet run --project src/Identity.Api --launch-profile Identity.Api
```

Форма строки для локальной БД: `Host=localhost;Port=54320;Database=identity;Username=identity;Password=<ваш пароль из .env>`.
Без корректной конфигурации API не стартует. API: `http://localhost:5080`. Проверка:

```sh
curl --fail http://localhost:5080/health/live
curl --fail http://localhost:5080/health/ready
curl --fail http://localhost:5080/openapi/v1.json
```

`/health/live` возвращает `200` и `{"status":"healthy"}`. OpenAPI доступен только
в Development. Launch profile включает Development; без профиля среда по умолчанию
Production, URL настраивается через `ASPNETCORE_URLS`/`ASPNETCORE_HTTP_PORTS`.
`/health/ready` проверяет подключение к PostgreSQL (200/503), не состояние схемы.
Миграции выполняются отдельной командой, никогда автоматически при старте API.
При запуске с SDK регистрация и worker по умолчанию выключены. Для включения
см. [конфигурацию доставки](docs/deployment.md#регистрация-и-письма).

## Локальный запуск через Compose

Скопируйте `.env.example` в `.env` и задайте **свой случайный** `POSTGRES_PASSWORD`.
Без него Compose откажется запускаться. Например, в PowerShell:

```powershell
Copy-Item .env.example .env
$identityPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
(Get-Content .env) -replace '^POSTGRES_PASSWORD=.*$', "POSTGRES_PASSWORD=$identityPassword" | Set-Content .env
Remove-Variable identityPassword
New-Item -ItemType Directory -Path secrets -Force | Out-Null
if (-not (Test-Path secrets/outbox-dev.key)) {
    [System.IO.File]::WriteAllBytes((Join-Path (Get-Location) 'secrets/outbox-dev.key'), [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}
if (-not (Test-Path secrets/jwt-dev.pem)) {
    $identityRsa = [System.Security.Cryptography.RSA]::Create(3072)
    [System.IO.File]::WriteAllText((Join-Path (Get-Location) 'secrets/jwt-dev.pem'), $identityRsa.ExportPkcs8PrivateKeyPem())
    $identityRsa.Dispose()
    Remove-Variable identityRsa
}
```

Это первоначальная настройка: при обычном запуске сохраняйте существующие `.env`
и ключ. Не добавляйте их в Git. Сначала поднимите зависимости:

```sh
docker compose config --quiet
docker compose up --detach --wait postgres mailpit
```

Примените миграцию SDK-командой из предыдущего раздела с host `localhost`, портом
54320 и паролем из `.env`. Затем запустите API:

```sh
docker compose up --build --detach --wait api
curl --fail http://localhost:5080/health/live
curl --fail http://localhost:5080/health/ready
curl --fail http://localhost:5080/openapi/v1.json
docker compose exec -T postgres pg_isready -U identity -d identity
docker compose ps
```

Локальные адреса (порты можно изменить в `.env`):

| Сервис | Адрес |
| --- | --- |
| API | `http://localhost:5080` |
| PostgreSQL 17 | `localhost:54320`, database/user: `identity` |
| Mailpit UI | `http://localhost:8025` |
| Mailpit SMTP | `localhost:1025` |

Все опубликованные порты привязаны к `127.0.0.1`. Контейнер API работает без root.
PostgreSQL использует именованный volume; `docker compose down` останавливает окружение
с сохранением данных. `docker compose down --volumes` также **удаляет локальные данные БД**.
Изменение пароля в `.env` не меняет пароль уже инициализированного PostgreSQL volume.
API получает настройки БД/SMTP через env, ключ — через secret mount.
Внутри Compose используются `postgres:5432` и `mailpit:1025`.
Worker доставляет письма в Mailpit; успешная доставка очищает encrypted payload.
Compose предназначен для разработки: Development и Mailpit без SMTP-аутентификации.

Сценарий register → Mailpit → verify → login, resend и curl-команды описаны в [docs/api.md](docs/api.md).
Страница `localhost:3000/verify` из dev-шаблона не входит в Identity: скопируйте токен
из fragment ссылки в Mailpit и отправьте POST verify либо настройте собственную страницу/BFF.

## Структура и проверки

```text
src/Identity.Domain          сущности, инварианты и переходы состояний
src/Identity.Application     CQRS, Register/VerifyEmail/RequestEmailVerification/Login handlers/validators
src/Identity.Infrastructure  EF/Npgsql, PBKDF2, AES-GCM, RS256/JWKS, SMTP, outbox worker
src/Identity.Api             composition root, диспетчеры, MVC Controllers, ProblemDetails
tests/Identity.UnitTests         Domain, CQRS, конфигурация и архитектура
tests/Identity.IntegrationTests  PostgreSQL/Testcontainers, миграции, CRUD, транзакции, конкуренция
tests/Identity.ApiTests          HTTP smoke, readiness 503 и ProblemDetails
docs/                           архитектура, API, безопасность, запуск
```

CI-ready команды из корня репозитория:

```sh
dotnet restore Identity.slnx
dotnet tool restore
dotnet build Identity.slnx --no-restore --configuration Release
dotnet test Identity.slnx --no-build --no-restore --configuration Release
dotnet format Identity.slnx --no-restore --verify-no-changes
docker build --tag identity:local .
# PowerShell 7.4+: изолированный end-to-end smoke этапа 3 (временные ресурсы удаляются)
pwsh -NoProfile -File scripts/Test-Stage3Smoke.ps1
```

Настройки компилятора общие: nullable, warnings-as-errors, C# 14; версии NuGet
фиксируются в `Directory.Packages.props`. Локальный `dotnet-ef` фиксирован в tool manifest.
Проверка snapshot (с заданной `Database__ConnectionString`):

```sh
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --output migration.sql
```

`migration.sql` — локальный результат, его не нужно добавлять в репозиторий.
EF InMemory и SQLite не используются вместо PostgreSQL.
CI workflow и полная продуктовая поставка относятся к этапу 7.

Подробности: [архитектура](docs/architecture.md), [HTTP API](docs/api.md),
[безопасность](docs/security.md), [запуск и конфигурация](docs/deployment.md).

REST token protocol Identity **не является OAuth 2.0/OpenID Connect**
и не предоставляет стандартизованный SSO. Браузерным приложениям потребуется BFF;
refresh tokens нельзя сохранять в localStorage/sessionStorage или доступных JavaScript cookie.
