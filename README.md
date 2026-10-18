# Identity

Самостоятельный self-hosted сервис учётных записей и аутентификации на .NET 10.
Источник требований — [Identity_Codex_Spec.md](Identity_Codex_Spec.md).

Реализован **этап 0: solution и каркас архитектуры**. Сейчас API предоставляет
только проверку живости. Регистрация, аутентификация, CQRS, хранение данных и отправка
писем ещё не реализованы: они добавляются отдельными этапами по спецификации.

## Требования

- .NET SDK 10.0 (stable; `global.json` допускает новые feature bands 10.0).
- Docker Engine с Linux containers и Docker Compose v2 для локального окружения.

## Запуск API без Docker

```sh
dotnet restore Identity.slnx
dotnet build Identity.slnx --no-restore --configuration Release
dotnet test Identity.slnx --no-build --no-restore --configuration Release
dotnet run --project src/Identity.Api --launch-profile Identity.Api
```

API: `http://localhost:5080`. Проверка:

```sh
curl --fail http://localhost:5080/health/live
curl --fail http://localhost:5080/openapi/v1.json
```

`/health/live` возвращает `200` и `{"status":"healthy"}`. OpenAPI доступен только
в Development. Launch profile включает Development; без профиля среда по умолчанию
Production, URL настраивается через `ASPNETCORE_URLS`/`ASPNETCORE_HTTP_PORTS`.
На этапе 0 API не подключается к PostgreSQL и SMTP, поэтому для этого запуска они не нужны.

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
PostgreSQL и Mailpit поднимаются для следующих этапов; на этапе 0 API их не использует.
Compose предназначен для разработки: Development и Mailpit без SMTP-аутентификации.

## Структура и проверки

```text
src/Identity.Domain          независимое ядро (пока без сущностей)
src/Identity.Application     слой use cases и AddApplication
src/Identity.Infrastructure  инфраструктура и AddInfrastructure
src/Identity.Api             composition root, MVC Controllers, HTTP
tests/Identity.UnitTests         проверки project references и изоляции слоёв
tests/Identity.IntegrationTests  запуск host, MVC discovery и OpenAPI dev/prod
tests/Identity.ApiTests          HTTP smoke и ProblemDetails
docs/                           архитектура, API, безопасность, запуск
```

CI-ready команды из корня репозитория:

```sh
dotnet restore Identity.slnx
dotnet build Identity.slnx --no-restore --configuration Release
dotnet test Identity.slnx --no-build --no-restore --configuration Release
dotnet format Identity.slnx --no-restore --verify-no-changes
docker build --tag identity:local .
```

Настройки компилятора общие: nullable, warnings-as-errors, C# 14; версии NuGet
фиксируются в `Directory.Packages.props`. EF Core и миграций на этапе 0 нет.
IntegrationTests сейчас проверяют только композицию приложения; тесты хранения
с реальным PostgreSQL/Testcontainers появятся с этапом 1. EF InMemory не используется.
CI workflow и полная продуктовая поставка относятся к этапу 7.

Подробности: [архитектура](docs/architecture.md), [HTTP API](docs/api.md),
[безопасность](docs/security.md), [запуск и конфигурация](docs/deployment.md).

Планируемый REST token protocol Identity **не является OAuth 2.0/OpenID Connect**
и не предоставляет стандартизованный SSO. Браузерным приложениям потребуется BFF;
refresh tokens нельзя сохранять в localStorage/sessionStorage или доступных JavaScript cookie.
