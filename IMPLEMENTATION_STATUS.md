# Отчёт по этапу 1

Дата: 2026-10-19. Источник требований: `AGENTS.md`, `Identity_Codex_Spec.md` целиком,
для этого этапа — разделы 3–5, 7, 9–10 и формат отчёта из раздела 13.

## 1. Этап

**1 — Domain, EF Core и собственный CQRS. Завершён.**

До начала работы реализован только этап 0: это подтверждено исходниками и коммитом
`d7c4f94` (`Solution, projects, Docker, base API`). Предыдущие коммиты `c8260bf`,
`29acf4a`, `ccce948` содержали инструкции/начальную документацию. Рабочее дерево
в начале этапа 1 было чистым. Этапы 2–7 до этой работы не были реализованы и не начаты.

## 2. Реализовано

- `src/Identity.Domain/Entities/`: User, Credential, Session, RefreshToken,
  OneTimeToken, OutboxMessage, SecurityAuditEvent, без public setters; фабрики,
  переходы состояний, проверки времён/лимитов и single-use в памяти.
- `src/Identity.Domain/Common/`, `Enums/`, `Exceptions/`, `ValueObjects/`:
  централизованная нормализация email, enum состояний/назначений, безопасные доменные
  ошибки, типизированные encrypted email envelope и whitelisted audit metadata.
- `src/Identity.Application/Abstractions/Cqrs/`, `Common/`: ICommand/IQuery,
  handlers, dispatchers, validators, Unit, FieldValidationError и ошибки приложения.
- `src/Identity.Application/Abstractions/Persistence/`: конкретные User/Credential
  repositories, минимальный UnitOfWork/transaction, readiness probe.
- `src/Identity.Application/Abstractions/Security/`: контракты password hash/verify
  с rehash result, token generator/hash, JWT issuance и outbox payload protection.
  Криптографических реализаций или заглушек не добавлено.
- `src/Identity.Application/Features/Health/GetDatabaseReadiness/`:
  реальный read-only query/handler, явно зарегистрированный в AddApplication;
  там же зарегистрирован заменяемый TimeProvider.System.
- `src/Identity.Api/Dispatching/`: scoped command/query dispatchers; validators
  выполняются до GetRequiredService конкретного handler, cancellation передаётся дальше.
- `src/Identity.Api/Middleware/ApiExceptionHandler.cs`, `Program.cs`:
  единые ProblemDetails для CQRS/domain/concurrency/unexpected errors и MVC validation,
  traceId, безопасное логирование типа неожиданного исключения; DI graph/scopes validation.
- `src/Identity.Api/Controllers/HealthController.cs`: `/health/ready` через query
  dispatcher и реальный PostgreSQL; `/health/live` сохранён без обращения к БД.
- `src/Identity.Infrastructure/Persistence/`: IdentityDbContext, семь явных
  Fluent API configurations, tracked repositories для команд, UnitOfWork, probe,
  DatabaseOptions/ValidateOnStart и design-time factory.
- `src/Identity.Infrastructure/Persistence/Migrations/20261018205035_InitialIdentity.cs`,
  designer и snapshot: все таблицы раздела 5, FK, unique/обычные/partial indexes,
  check constraints, timestamptz, snake_case и optimistic concurrency через xmin.
- `.config/dotnet-tools.json`, `Directory.Packages.props`, Infrastructure csproj:
  локальный dotnet-ef/EF Core 10.0.13, Npgsql EF provider 10.0.3, Testcontainers 4.16.0.
- Тесты: `DomainTests.cs`, `DispatcherTests.cs`, `ConfigurationTests.cs`, расширенный
  `ArchitectureTests.cs`, `PostgreSqlFixture.cs`, `PersistenceTests.cs`, обновлённые
  bootstrap/API tests. Полная база EF InMemory/SQLite не используется.
- Обновлены README, четыре файла docs, Compose, appsettings и `.env.example`.
  Строка подключения обязательна извне. `.editorconfig` распознаёт EF migrations
  как generated code; `.gitignore` исключает локальный SQL script.

## 3. Решения

- Сохранён граф project references этапа 0: Domain standalone; Application -> Domain;
  Infrastructure -> Application + Domain; Api -> Application + Infrastructure.
  Domain/Application не используют ASP.NET, EF, Npgsql, IConfiguration или HttpContext.
- Реальный демонстрационный query обслуживает предусмотренный разделом 7 readiness
  endpoint. Искусственного production business endpoint не добавлено. Probe не
  материализует сущности, ChangeTracker остаётся пустым. Репозитории возвращают
  tracked entities для команд; будущие read-only entity queries должны использовать AsNoTracking.
- В CQRS нет MediatR, reflection/dynamic в hot path, assembly scanning, behaviors
  или notifications. Текущий handler зарегистрирован явно один раз и scoped.
- Времена передаются доменным методам явно; TimeProvider можно подменять в тестах.
  SQL-пределы сроков заданы в часах, чтобы 30/7 суток означали 720/168 часов даже при DST.
- xmin — базовая защита от потерянного обновления. Она не заменяет полный refresh
  rotation/replay use case этапа 4. Сохранение проигравшего изменения токена откатывает
  и добавленную в том же SaveChanges замену; это проверено конкурентным тестом.
  [Описание xmin в Npgsql](https://www.npgsql.org/efcore/modeling/concurrency.html).
- UnitOfWork не является generic repository. Он разделяет один DbContext с адаптерами,
  предоставляет SaveChanges и явную транзакцию. После rollback/concurrency failure
  нужно завершить scope, не повторно использовать старый ChangeTracker.
- Контракт outbox предусматривает шифрование всего содержимого будущего письма,
  включая чувствительные данные: на этапе 1 есть только модель envelope, никаких реальных писем/шифрования.
  Payload nullable только после обработки и очищается сразу (retention ciphertext = 0).
  Audit metadata сейчас допускает только целочисленный неотрицательный attemptCount.
- Миграции выполняются отдельно, не на старте API. InitialIdentity до публикации
  уточнялась на созданной пустой smoke-БД; существующих пользовательских БД/миграций
  в репозитории не было. В итоговых исходниках одна начальная миграция.
- Исправлен блокирующий дефект этапа 0: общий Produces(application/json) принудительно
  менял Content-Type MVC validation errors. JSON formatter сохранён, базовый Produces
  удалён; ответ снова application/problem+json. Поведение проверено HTTP-тестом.
- Расхождений реализации с объёмом этапа 1 не выявлено. Публичные register/login,
  JWT/JWKS, SMTP/outbox worker, reset и profile use cases не реализовывались.

## 4. Проверки

Среда: Windows, .NET SDK 10.0.402/runtime 10.0.13, Docker Desktop Linux Engine,
PostgreSQL 17 (`postgres:17-alpine`). Применимые проверки выполнены.

Точные команды из корня репозитория:

```sh
dotnet restore Identity.slnx
dotnet tool restore
dotnet build Identity.slnx --no-restore --configuration Release
dotnet test Identity.slnx --no-build --no-restore --configuration Release
dotnet format Identity.slnx --no-restore
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet ef migrations add InitialIdentity --project src/Identity.Infrastructure --output-dir Persistence/Migrations --configuration Release
dotnet ef database update --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations list --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
docker compose --env-file .env.smoke --project-name identity-stage1-smoke config --quiet
docker compose --env-file .env.smoke --project-name identity-stage1-smoke up --build --detach --wait --wait-timeout 90
docker compose --env-file .env.smoke --project-name identity-stage1-smoke exec -T api id
docker compose --env-file .env.smoke --project-name identity-stage1-smoke exec -T postgres pg_isready -U identity -d identity
docker compose --env-file .env.smoke --project-name identity-stage1-smoke exec -T postgres psql -U identity -d identity -At -c 'SELECT * FROM "__EFMigrationsHistory"'
docker compose --env-file .env.smoke --project-name identity-stage1-smoke ps
docker compose --env-file .env.smoke --project-name identity-stage1-smoke down --volumes
git diff --check
```

При уточнении неопубликованной initial migration **только на временной smoke-БД**
выполнялись также `dotnet ef database update 0 --project src/Identity.Infrastructure
--configuration Release --no-build` и `dotnet ef migrations remove --project
src/Identity.Infrastructure --configuration Release`, затем повторные add/update.
В финальном update для пересборки миграции флаг --no-build опускался.

Результаты:

- Restore solution и tool: успешно.
- Финальная Release build: **0 ошибок, 0 предупреждений**.
- **83 passed, 0 failed, 0 skipped**: UnitTests 35, ApiTests 9, IntegrationTests 39.
- Integration tests применяют реальные миграции, выполняют Up -> Down -> Up,
  проверяют отсутствие model drift, семь таблиц, индексы, FK, check constraints,
  JSON typing/whitelist, UTC и DST, CRUD, атомарность SaveChanges и явные commit/rollback.
- Конкурентные вставки одного normalized_email дают одну запись; xmin предотвращает
  потерю обновления login counter, двойной consume one-time token и двойное обновление
  refresh row. Это тесты хранилища, не готовых register/refresh API следующих этапов.
- CQRS tests: handler вызывается один раз, validators блокируют invalid request,
  cancellation не теряется; runtime/project architecture tests успешны.
- API tests: live, dev/prod OpenAPI, ready 503 при недоступной БД, 400/404/405/409/500,
  общий validation contract и отсутствие сообщения/типа исключения в 500 response.
  Startup отвергает отсутствующую/небезопасную конфигурацию; scopes проверены.
- Format verify: успешно после упорядочивания using. Первый build выявил конфликт
  patch-версий EF Relational; устранён явным выравниванием Core/Relational/Design.
  Первые тесты обнаружили Content-Type дефект и форматирование JSON в test SQL;
  исправлено, финальный полный запуск зелёный.
- EF CLI: начальная миграция применена, model drift отсутствует, idempotent SQL
  сгенерирован. В истории smoke-БД: `20261018205035_InitialIdentity|10.0.13`.
- Docker: окончательный исходный код восстановлен и опубликован в Linux build stage;
  Compose стартовал успешно. API uid=1654(app), PostgreSQL accepting connections.
- HTTP smoke выполнялся через Invoke-WebRequest/Invoke-RestMethod:
  `http://localhost:5181/health/live` -> 200 healthy,
  `http://localhost:5181/health/ready` -> 200 ready,
  `http://localhost:5181/openapi/v1.json` содержит оба GET;
  `http://localhost:18026/` (Mailpit) -> 200.
- Временный пароль генерировался вне Git; .env.smoke, migration.sql, smoke containers,
  сеть и только созданный smoke volume удалены. Testcontainers также завершён.
- git diff --check и поиск запрещённых конструкций/заглушек: без ошибок.
  Невыполненных применимых проверок нет; тесты последующих этапов не заявляются.

## 5. Безопасность

В исходниках нет production keys/паролей. В БД предусмотрены отдельные password_hash,
только token_hash и encrypted-only outbox envelope. Имена полей/JSON-типы ограничены
на уровне модели и БД; audit не допускает произвольную PII в metadata. Уникальность,
ссылочная целостность, временные инварианты и optimistic concurrency проверены на PostgreSQL.
Readiness не раскрывает connection string/ошибки БД и должен быть закрыт proxy/firewall;
локальный Compose публикует порты только на loopback. Параметры/подробности ошибок БД
не логируются; generic exception response и безопасное логирование протестированы.

Конкретные PBKDF2, AES-256-GCM, JWT/keys, SMTP, outbox claim/retry, аудит use cases,
rate limits и полная auth-защита ещё не реализованы — относятся к последующим этапам.

## 6. Ограничения/блокеры

Блокеров этапа 1 нет. API требует Database__ConnectionString даже при использовании
только live; реальное соединение при старте не открывается и миграции не применяются.
Readiness проверяет соединение, а не актуальность схемы. Compose предназначен для dev.
Наличие security interfaces и тестовых envelope не означает наличия криптографической
реализации или безопасного готового authentication service. Полный register/verify,
login, refresh replay, profile/password lifecycle и SMTP ещё отсутствуют.
Изменения этапа 1 оставлены в рабочем дереве; commit/deploy не выполнялись.

## 7. Следующий этап

Рекомендован **этап 2 — Register + email verification**, только по отдельному запросу.
Этап 2 не начат.
