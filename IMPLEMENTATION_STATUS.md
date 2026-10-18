# Отчёт по этапу 0

Дата: 2026-10-19. Источник требований: `Identity_Codex_Spec.md`, разделы 3, 7, 10 и 13.

## 1. Этап

**0 — Solution и каркас архитектуры.** Реализован только этот этап.

## 2. Реализовано

- `Identity.slnx`: четыре production-проекта и три test-проекта.
- `src/Identity.Domain/Identity.Domain.csproj`: независимый слой без пакетов.
- `src/Identity.Application/`: ссылка на Domain, `AddApplication()`.
- `src/Identity.Infrastructure/`: ссылки на Application/Domain, `AddInfrastructure()`.
- `src/Identity.Api/`: composition root, проверка DI при сборке host, Controllers,
  базовый `ApiController` с `/api/v1/[controller]`, `HealthController` с `/health/live`,
  JSON DTO, OpenAPI только в Development, стандартные ProblemDetails с `code`/`traceId`.
- `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`:
  .NET 10, C# 14, nullable, warnings-as-errors, CPM и форматирование.
- `tests/Directory.Build.props` и три test-проекта: 13 тестов на зависимости слоёв,
  bootstrap MVC, OpenAPI dev/prod, HTTP live и ProblemDetails 404/405/500.
- `Dockerfile`, `compose.yaml`, `.dockerignore`, `.gitignore`, `.env.example`:
  multistage build, non-root API, PostgreSQL 17 и Mailpit для локальной разработки.
- Обновлён `README.md`; добавлены `docs/architecture.md`, `docs/api.md`,
  `docs/security.md`, `docs/deployment.md`. Существующие AGENTS.md и спецификация не изменены.

## 3. Решения

- Собственный CQRS, доменные сущности, TimeProvider, EF Core и миграции оставлены
  этапу 1 согласно разделу 10. MediatR и альтернативные mediator frameworks отсутствуют.
- `AddApplication`/`AddInfrastructure` — готовые точки композиции без искусственных
  сервисов: на этапе 0 use cases и инфраструктурные адаптеры ещё не требуются.
- `/health/live` реализован MVC Controller и означает только живость HTTP-процесса.
  `/health/ready` и проверки БД в API пока отсутствуют. Базовый `ApiController`
  задаёт v1 route для будущих операций, не создавая демонстрационных endpoints.
- Использован встроенный OpenAPI (`Microsoft.AspNetCore.OpenApi`), без UI-пакета.
  Такой вариант соответствует официальной документации ASP.NET Core:
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0
- IntegrationTests этапа 0 проверяют композицию host, а не хранение данных.
  PostgreSQL проверен отдельно в Docker smoke; это не заменяет интеграционные
  тесты схемы и транзакций, запланированные этапом 1.
- Имена Compose/API/БД относятся только к Identity. `compose.yaml` выбран по структуре
  раздела 3; Compose CLI поддерживает его как основной файл конфигурации.
- Для dev используются stable image tags; закрепление проверенных digests и полная
  production-поставка остаются этапу 7. Функциональных расхождений с этапом 0 ТЗ не выявлено.

## 4. Проверки

Среда: Windows, .NET SDK 10.0.402, runtime 10.0.13, Docker Desktop с Linux Engine.

Команды из корня репозитория:

```sh
dotnet restore Identity.slnx
dotnet build Identity.slnx --no-restore --configuration Release
dotnet test Identity.slnx --no-build --no-restore --configuration Release
dotnet format Identity.slnx --no-restore --verify-no-changes
docker build --tag identity:stage0-smoke .
docker compose --env-file .env.smoke --project-name identity-stage0-smoke config --quiet
docker compose --env-file .env.smoke --project-name identity-stage0-smoke up --build --detach --wait --wait-timeout 90
docker compose --env-file .env.smoke --project-name identity-stage0-smoke exec -T postgres pg_isready -U identity -d identity
docker compose --env-file .env.smoke --project-name identity-stage0-smoke exec -T api id
docker compose --env-file .env.smoke --project-name identity-stage0-smoke ps
docker compose --env-file .env.smoke --project-name identity-stage0-smoke down --volumes
git diff --check
```

Результаты:

- Restore: все 7 проектов восстановлены.
- Release build: **0 ошибок, 0 предупреждений**.
- Tests: **13 passed, 0 failed, 0 skipped**: UnitTests 6, IntegrationTests 3, ApiTests 4.
- Первый `dotnet format --verify-no-changes` обнаружил порядок двух `using`;
  исправлено, повторная проверка успешна.
- Docker build: успешны restore и Release publish внутри Linux build stage.
- Compose config и up: успешны; подняты три сервиса.
- HTTP smoke выполнен следующими PowerShell-командами с проверками status/content:

  ```powershell
  Invoke-WebRequest -Uri http://localhost:5180/health/live
  Invoke-RestMethod -Uri http://localhost:5180/openapi/v1.json
  Invoke-WebRequest -Uri http://localhost:18025/
  ```

  Получены `200 {"status":"healthy"}`, OpenAPI с GET `/health/live`, Mailpit UI 200.
- `pg_isready`: `accepting connections`; пользователь API: `uid=1654(app)`, не root.
- Изолированный smoke project использовал порты 5180/55432/18025/11025 и временный
  случайный пароль в игнорируемом `.env.smoke`. После проверки контейнеры, сеть,
  только созданный тестовый volume и файл пароля удалены.
- `git diff --check`: ошибок whitespace нет. Дополнительный поиск по исходникам
  не обнаружил запрещённых библиотек, Minimal API business handlers, `EnsureCreated`
  или версий в `PackageReference`.
- **Миграции: не применимо** — DbContext, EF Core и миграций по объёму этапа 0 нет.
  `dotnet ef` и миграционные тесты не запускались; они обязательны на этапе 1.
- В начале sandbox ограничивал служебные файлы .NET и доступ к Docker pipe;
  проверки выполнены после предоставления доступа. Невыполненных применимых
  проверок этапа 0 не осталось.

## 5. Безопасность

Секретов/ключей в исходниках нет; пароль PostgreSQL обязателен извне. Git исключает
локальные env/ключи, Docker context ограничен build-файлами и src. Порты Compose
опубликованы только на loopback, API запускается без root, OpenAPI отключён в Production.
Ответ 500 протестирован на отсутствие сообщения исключения/его типа; HTTP-ошибки
содержат traceId. CORS и логирование body не подключены.

Защита учётных записей и токенов, SMTP/outbox encryption, audit, rate limits, TLS/proxy
и полная таксономия ошибок остаются соответствующим этапам ТЗ.

## 6. Ограничения/блокеры

Блокеров этапа 0 нет. Это bootstrap, а не готовый к публичной эксплуатации сервис
аутентификации: нет пользовательских операций, схемы БД, JWT/JWKS, SMTP sender,
CQRS и readiness. Compose — dev-окружение с Mailpit. Полный CI workflow и production
deployment guidance относятся к этапу 7. API не заявляет поддержку OAuth/OIDC/SSO.

## 7. Следующий этап

Рекомендован **этап 1 — Domain, EF Core и собственный CQRS**, только после отдельного
запроса человека. Этап 1 не начат.
