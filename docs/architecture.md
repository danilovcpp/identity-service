# Архитектура Identity 1.0.0

Этап10: отдельные command slices для admin writes и public invitation acceptance;
AdminWriteAuthorization повторно проверяет actor/session/role внутри транзакции.
Общий порядок: advisory lock -> sorted actor/target user rows -> sessions/tokens.
InvitationIssuer переиспользуется invite/resend без вызова handler из handler. Domain
сохраняет prior status; Infrastructure отвечает за purpose-specific locks, SMTP/AES outbox
и startup options. [Контракт и решения](admin-write-api.md). Новых NuGet dependencies нет.

Этап9: пять admin query slices с typed validators/DTO и явным DI. Отдельный
IAdminReadRepository реализован Infrastructure через AsNoTracking Select/GroupBy;
никаких EF/ASP.NET типов в Application и DbContext в Controllers. Count/page и dashboard
согласованы RepeatableRead snapshot. Все сортировки фиксированы; ID — tie-breaker.
AdminReadIndexes добавляет только индексы. [Контракт, SQL и ограничения](admin-read-api.md).

Дополнение v1.1, этап 8: `Identity.AdminCli` — локальный composition root Application +
Infrastructure без HTTP/worker startup. Системные роли и admin sessions — отдельные Domain
entities, Application/Admin use cases зарегистрированы явно. Infrastructure содержит
EF repositories, CookieAuthentication и Data Protection (shared ASP.NET Core framework);
Domain/Application не получили ASP.NET/EF dependencies. API Controllers/Admin используют
собственный CQRS. Cookie validation выполняет Application command в отдельном scope;
последующий MVC use case не наследует tracked entities завершённой validation-транзакции.
Общий порядок потери admin eligibility: advisory lock → user → session rows. Обычные
login/session операции начинают с user lock. [Контракт и решения](admin-access.md).

Этап 7 завершает поставку без изменения business handlers/схемы: version 1.0.0,
SHA-pinned CI, secret guard, dev/production templates и release notes. Docker image
содержит только publish output, работает non-root и проверяет liveness отдельным
`--health-check` процессом того же assembly, до построения host/DI. Probe использует
только HttpClient/JSON к фиксированному loopback, не загружает keys и не делает EF/SMTP IO.
Нового HTTP endpoint или зависимости Domain/Application не добавлено.

Этап 6 добавляет API transport options/allowlist, RequestSecurityMiddleware с единым
OperationContext/trace/header/scope и безопасными HTTP metrics. Forwarded headers
обрабатываются до HTTPS-проверки и IP limiter. В production HTTP business requests
отклоняются; health endpoints оставлены для internal probes. HSTS/security/correlation
headers восстанавливаются OnStarting даже после Response.Clear при исключении.
JsonProblemDetailsWriter регистрируется **до MVC writers** и обеспечивает один
контракт независимо от Accept; model binding не отражает входные значения/JSON paths.
Business endpoints по-прежнему MVC, бизнес-логика не перенесена в middleware.

Infrastructure AuthMetrics получает только сохранённые SecurityAuditEvent: UoW
откладывает публикацию до успешного CommitAsync, rollback/dispose очищают pending.
Implicit SaveChanges публикует после commit EF. Метрики и безопасные logs best-effort,
не являются источником истины и не изменяют результат committed auth operation.
Источник истины — транзакционный PostgreSQL audit; user/client/trace IDs не labels.
Domain/Application не зависят от ILogger, Meter, ASP.NET или EF.

SecurityMaintenanceWorker создаёт scope на итерацию и выполняет три bounded batches
с SKIP LOCKED: audit по occurred_at, one-time tokens по expiry, processed outbox по
processed_at. Затем обновляет sampled gauge активных DB-сессий. Pending/dead-letter
outbox и refresh history не затрагиваются. Refresh cleanup остаётся отдельным механизмом
с прежней гарантией replay evidence. Новая миграция SecurityMaintenanceIndex добавляет
только partial index processed_at; существующие audit/expiry indexes используются повторно.

Этап 5 добавляет GetCurrentUser, UpdateProfile, ChangePassword, RequestPasswordReset,
ResetPassword и DeactivateAccount с отдельными handlers/validators и явной DI.
UsersController/AuthController только преобразуют HTTP DTO и вызывают собственные
CQRS dispatchers. Application получает trusted user/session IDs без HttpContext.
GetCurrentUser использует IProfileRepository: одна AsNoTracking projection активного
user и действующей собственной сессии, без credential/token fields.

UpdateProfile/change/deactivate повторно проверяют actor под блокировками user →
current session. Общий scoped SessionRevoker используется также LogoutAll: отзывает
все сессии и refresh rows в той же UoW/явной транзакции. Пароль/статус, отзыв,
аннулирование reset links и audit фиксируются вместе. Domain и схема не менялись;
этап 5 использует существующие PasswordReset purpose и revoke/audit enums.

RequestPasswordReset блокирует user, аннулирует старые links и атомарно сохраняет
30-minute token hash вместе с AES-GCM outbox message. Unknown/ineligible accounts
выполняют генерацию/encryption без записи и получают тот же 202. Reset сначала
читает только immutable owner ID, затем блокирует user и заново читает token FOR UPDATE.
Этот порядок общий с login/refresh/profile и сериализует разные reset tokens одного
аккаунта. Tracked consumed token исключён из bulk invalidation остальных, чтобы
не менять его xmin до SaveChanges. Links с будущим createdAt тоже аннулируются;
consumedAt ограничен снизу createdAt для соблюдения SQL constraints при рассинхронизации часов.

IPasswordResetSettings/IPasswordResetEmailFactory/IPasswordResetRepository изолируют
Application от options, SMTP, AES и EF. Отдельный PasswordReset:Enabled сохраняет
независимость от публичной регистрации. Worker принимает только известные типы
email_verification/password_reset; доставка использует прежние retry/claim/cleanup.
Транспорт и key ring проверяются при любом включённом producer/worker; URL template
нужен только включённому producer. Новых packages или migrations нет.

Этап 4 добавляет отдельные команды/handlers Refresh, Logout, LogoutAll, RevokeSession,
query GetSessions и их явные DI registrations. Login/refresh используют общий
TokenPairResult; JSON контракт login не меняется. Actor sub/sid извлекаются в API
только из проверенных claims, в Application приходят Guid, без HttpContext.
SessionsController/AuthController не получают DbContext или repositories.

ISessionRepository возвращает immutable owner IDs через AsNoTracking до транзакции,
затем все мутации придерживаются user FOR UPDATE → session FOR UPDATE → refresh FOR UPDATE.
Tracked token читается только после блокировок, чтобы ожидающий конкурентный запрос
видел committed consumed state. Порядок общий с Login; LogoutAll сериализован также
с Login/refresh всех сессий пользователя. GetSessions — read-only projection/AsNoTracking.

Consumed/replaced, новая строка refresh, lastUsedAt и audit фиксируются одной транзакцией.
Replay фиксирует revoke session/all token rows и audit **до** generic 401. Bulk token
updates используют ту же явную транзакцию; tracked token после них не модифицируется.
См. [EF Core bulk operations/transactions](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete).
Сбой до commit откатывает всё; потеря ответа после commit не делает старый token usable.

Session.Audience сохраняется при login; mismatch с client configuration запрещает
refresh и собственный JWT доступ. Legacy NULL не интерпретируется как текущая audience.
Миграция SessionAudienceBinding добавляет binding/index и NO ACTION self-FK, сохраняя
same-family replacement constraint. Полная цепочка удаляется только после срока
семейства + retention. [PostgreSQL FK constraints](https://www.postgresql.org/docs/17/ddl-constraints.html).
SessionCleanupWorker создаёт scoped processor; тот выбирает bounded family batch с
SKIP LOCKED и удаляет family/session атомарно. Domain/Application не зависят от worker/EF.

Этап 3 добавляет LoginCommand/validator/handler в Application. ILoginSettings,
ILoginPasswordVerifier, ITokenService и ILoginRepository сохраняют независимость
от IConfiguration/JWT/EF/HTTP. Handler использует существующие user/credential
repositories и UoW; один row lock user сериализует counters, rehash и создание session.
LoginRepository выполняет read-only проверку активного user/session через AsNoTracking.
На этапе 3 миграции не менялись; этап 4 добавляет audience binding и индекс cleanup.

Infrastructure содержит options/clients, immutable RSA key ring, JwtTokenService
на Microsoft.IdentityModel.JsonWebTokens и публичную JWK projection. Key ring загружается
как IHostedService до запросов; ключи не генерируются API. Криптопровайдеры этих RSA
не используют глобальный signature-provider cache: он может удерживать освобождённый
RSA при повторном создании host в одном процессе. Подпись остаётся библиотечной.

API конфигурирует JwtBearer и его OnTokenValidated (обязательные claims + scoped
проверка активности по ILoginRepository), единые 401/403 ProblemDetails и default MVC
AuthorizeFilter. Публичные auth actions явно AllowAnonymous, поэтому новые auth actions
будут защищены по умолчанию. DiscoveryController публикует только public JWK Set.
Тестовые protected/external API controllers находятся исключительно в test assembly.

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
ConcurrencyConflictException. Для refresh rotation/replay этап 4 дополнительно
использует явные блокировки пользователя/сессии/токена и отзыв всего семейства.
См. [официальное описание xmin в Npgsql](https://www.npgsql.org/efcore/modeling/concurrency.html).

Конфигурация Database options проверяется ValidateOnStart; Domain/Application
не получают IConfiguration. Миграции запускаются отдельно через design-time factory.
Security-интерфейсы реализованы для PBKDF2, случайных токенов и outbox encryption;
JWT реализован на этапе 3.
MediatR, generic repository, ASP.NET Core Identity user management не подключены.

Тесты проверяют зависимости проектов и сборок, Domain, CQRS, cancellation,
HTTP errors/validation, bootstrap и options. Testcontainers PostgreSQL 17 применяется
для Up/Down миграции, CRUD, FK/индексов/check constraints, UTC, транзакций, конкурентного
insert и optimistic concurrency. InMemory provider не используется.
Ограничения 30/7 дней выражены в SQL как 720/168 часов: пределы не зависят от DST
и часовой зоны соединения PostgreSQL. Это проверено отдельным regression test.

Этап 2 добавляет AuthController и три отдельных command/handler/validator:
Register, VerifyEmail, RequestEmailVerification. Контроллеры работают через существующий
dispatcher. Application зависит только от конкретных persistence/security/notification
интерфейсов. Password policy и NFKC общие для validation/hash/verify; криптографические
примитивы Microsoft PasswordHasher и AesGcm, а также SMTP/MailKit находятся в Infrastructure.

Регистрация выполняет hash/encrypt также для дубликата email и полагается на unique
индекс БД; UnitOfWork переводит только его нарушение в DuplicateEmailException, которое
handler скрывает за generic 202. Один SaveChanges и явный transaction объединяют
user/credential/token/outbox/audit. Verify/resend блокируют строку user; это сериализует
подтверждение разных токенов одного пользователя без вызовов handler из handler.

EmailOutboxWorker получает scoped processor на запись. Processor держит транзакцию
и строковый lock `FOR UPDATE SKIP LOCKED` на время ограниченной по timeout SMTP-отправки,
затем фиксирует success/cleanup или failure/backoff. Это простой корректный claim для
малого потока, без lease/schema changes; длинное SMTP-соединение удерживает один DB connection.
При сбое после принятия письма SMTP и до DB commit возможен повтор — at-least-once.
Stable Message-Id не является гарантией дедупликации принимающим сервером.
