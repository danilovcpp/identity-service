# Identity

Самостоятельный self-hosted сервис учётных записей и аутентификации на .NET 10.
Источник требований — [Identity_Codex_Spec.md](Identity_Codex_Spec.md).

Реализованы **этапы 0 и 1: каркас, Domain, EF Core и собственный CQRS**.
Есть доменная модель, начальная миграция PostgreSQL, репозитории/UnitOfWork,
типизированные диспетчеры и проверки живости/готовности. Регистрация, login,
JWT/refresh и отправка писем добавляются следующими этапами по спецификации.

## Требования

- .NET SDK 10.0 (stable; `global.json` допускает новые feature bands 10.0).
- Docker Engine с Linux containers и Docker Compose v2 для локального окружения
  и интеграционных тестов (Testcontainers запускает настоящий PostgreSQL 17).

## Сборка и тесты

```sh
dotnet restore Identity.slnx
dotnet tool restore
dotnet build Identity.slnx --no-restore --configuration Release
dotnet test Identity.slnx --no-build --no-restore --configuration Release
```

Тесты запускают временный PostgreSQL, применяют миграцию и удаляют контейнер после
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

## Локальный запуск через Compose

Скопируйте `.env.example` в `.env` и задайте **свой случайный** `POSTGRES_PASSWORD`.
Без него Compose откажется запускаться. Например, в PowerShell:

```powershell
Copy-Item .env.example .env
$identityPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
(Get-Content .env) -replace '^POSTGRES_PASSWORD=.*$', "POSTGRES_PASSWORD=$identityPassword" | Set-Content .env
Remove-Variable identityPassword
```

Не добавляйте `.env` в Git. Запуск и проверка:

```sh
docker compose config --quiet
docker compose up --build --detach --wait
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
API получает строку подключения к PostgreSQL через env в Compose. После запуска
примените миграцию SDK-командой из предыдущего раздела, используя host `localhost`
и порт 54320; внутри Compose API использует host `postgres` и порт 5432.
Mailpit пока не используется приложением: отправка писем относится к этапу 2.
Compose предназначен для разработки: Development и Mailpit без SMTP-аутентификации.

## Структура и проверки

```text
src/Identity.Domain          сущности, инварианты и переходы состояний
src/Identity.Application     CQRS/validation/security/persistence контракты, readiness query
src/Identity.Infrastructure  EF Core/Npgsql, конфигурации, миграции, репозитории, UnitOfWork
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

Планируемый REST token protocol Identity **не является OAuth 2.0/OpenID Connect**
и не предоставляет стандартизованный SSO. Браузерным приложениям потребуется BFF;
refresh tokens нельзя сохранять в localStorage/sessionStorage или доступных JavaScript cookie.
