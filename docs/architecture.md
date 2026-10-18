# Архитектура Identity — этапы 0–1

Граф project references соответствует разделу 3 спецификации:

```text
Domain          -> нет зависимостей
Application     -> Domain
Infrastructure  -> Application, Domain
Api             -> Application, Infrastructure
```

Domain содержит User, Credential, Session, RefreshToken, OneTimeToken,
OutboxMessage и SecurityAuditEvent. Фабрики и методы переходов проверяют инварианты;
публичных setters нет. Email нормализуется единой функцией Trim/ToUpperInvariant
без правил отдельных провайдеров; control characters отклоняются. Время передаётся
в доменные методы явно и приводится к UTC. Слой не содержит NuGet-зависимостей.
Application использует только DI abstractions, без ASP.NET, EF, IConfiguration или HttpContext.

API — composition root. DI проверяет lifetime scopes и граф зарегистрированных
сервисов при сборке host во всех средах. Контроллер живости использует только HTTP DTO.
Абстрактный `ApiController` задаёт `[ApiController]` и route `api/v1/[controller]`
для бизнес-контроллеров следующих этапов. Сам по себе он endpoints не создаёт.
JSON задаётся стандартными MVC formatters. `Produces` на базовом контроллере не
переопределяет Content-Type ошибок ProblemDetails. HealthController использует
операционные маршруты `/health/live` и `/health/ready`.
Minimal API бизнес-endpoints отсутствуют; `MapOpenApi()` обслуживает только документацию.

CQRS использует собственные ICommand/IQuery, handlers, Unit и scoped dispatchers
в API. Диспетчеры последовательно вызывают типизированные validators, собирают
FieldValidationError и затем получают handler через GetRequiredService. В hot path
нет dynamic, MakeGenericType или reflection. Обработчик readiness явно зарегистрирован
один раз в `AddApplication`, без assembly scanning; DI и lifetime проверяются на старте.
`TimeProvider.System` зарегистрирован через TryAddSingleton и допускает замену в тестах.

Реальный read-only query `GetDatabaseReadinessQuery` вызывает через Application
интерфейс `IDatabaseReadinessProbe`; Infrastructure проверяет соединение EF/Npgsql.
Query не загружает и не меняет сущности. Дополнительных демонстрационных business
endpoints нет. Репозитории User/Credential возвращают tracked entities для будущих
команд; read-only entity queries должны использовать AsNoTracking при добавлении.

Infrastructure содержит IdentityDbContext, явные Fluent API конфигурации семи таблиц,
начальную миграцию, UserRepository, CredentialRepository и минимальный UnitOfWork.
Все адаптеры одного scope используют один DbContext. SaveChanges атомарен;
BeginTransactionAsync нужен для границы use case с несколькими сохранениями.
Незавершённая транзакция откатывается при async dispose. После rollback/ошибки
конкуренции следует завершить scope, не повторно использовать его ChangeTracker.

Для изменяемых строк настроен shadow concurrency token PostgreSQL xmin: потерянное
обновление вызывает DbUpdateConcurrencyException, а UnitOfWork переводит его в
ConcurrencyConflictException. Это базовая защита строки, не полный протокол refresh
rotation/replay; блокировки семейства и отзыв всей сессии относятся к этапу 4.
См. [официальное описание xmin в Npgsql](https://www.npgsql.org/efcore/modeling/concurrency.html).

Конфигурация Database options проверяется ValidateOnStart; Domain/Application
не получают IConfiguration. Миграции запускаются отдельно через design-time factory.
Security-интерфейсы заданы без фиктивных реализаций: PBKDF2/outbox encryption и JWT
появятся на этапах 2 и 3 соответственно.
MediatR, generic repository, ASP.NET Core Identity user management не подключены.

Тесты проверяют зависимости проектов и сборок, Domain, CQRS, cancellation,
HTTP errors/validation, bootstrap и options. Testcontainers PostgreSQL 17 применяется
для Up/Down миграции, CRUD, FK/индексов/check constraints, UTC, транзакций, конкурентного
insert и optimistic concurrency. InMemory provider не используется.
Ограничения 30/7 дней выражены в SQL как 720/168 часов: пределы не зависят от DST
и часовой зоны соединения PostgreSQL. Это проверено отдельным regression test.
