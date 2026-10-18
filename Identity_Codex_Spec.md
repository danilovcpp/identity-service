# Техническое задание: Identity

**Версия документа:** 1.0  
**Статус:** к поэтапной реализации  
**Продукт:** `Identity` — независимый self-hosted сервис учётных записей и аутентификации пользователей.  
**Адресат:** Codex, работающий в репозитории продукта.

---

## 0. Главная инструкция для Codex

Ты разрабатываешь самостоятельный мини-продукт **Identity**. Он не должен содержать названий, ссылок, зависимостей или бизнес-логики каких-либо внешних продуктов. Реализуй Clean Architecture, CQRS с собственным легковесным диспетчером, ASP.NET Core **Controllers** и PostgreSQL.

**Порядок работы:** выполняй **только этап, явно указанный в текущем запросе**. После этапа остановись, запусти доступные сборку/тесты, сообщи об изменённых файлах, результате проверок, миграциях, принятых решениях и нерешённых вопросах. Не начинай следующий этап автоматически. Не заменяй обязательное поведение заглушками, фиктивными сервисами или `TODO`. Если инфраструктура недоступна, честно укажи, какие проверки не выполнены. Не развёртывай проект во внешней среде и не публикуй секреты.

**Критерий качества:** простая поддерживаемая архитектура без чрезмерных абстракций; корректность и безопасность важнее скорости разработки. Не реализуй криптографические алгоритмы, JWT-подпись, SMTP-протоколы и хранение паролей самостоятельно: используй проверенные библиотеки, но **не** готовую систему управления пользователями.

## 1. Назначение и границы продукта

### 1.1. Цель

Реализовать небольшой независимый backend, который можно запускать на VPS или в Docker и использовать для регистрации, входа, управления профилем и сессиями пользователей из нескольких внешних приложений.

Identity отвечает на вопросы: **кто пользователь, как подтверждается его личность, какие у него активные сессии?**

### 1.2. В объёме версии 1.0

- Регистрация по email и паролю; email — единственный идентификатор входа.
- Подтверждение email и повторная отправка подтверждения.
- Login, выдача короткоживущего JWT access token и непрозрачного refresh token.
- Обновление токенов с rotation и обнаружением повторного использования.
- Logout текущей сессии; logout всех сессий.
- Просмотр и отзыв собственных активных сессий.
- Профиль (`GET /me`, изменение `displayName`).
- Смена и восстановление пароля.
- Деактивация собственной учётной записи с отзывом сессий.
- Конфигурируемый список клиентов и аудиторий JWT (без UI управления ими).
- Публикация открытых JWK-ключей, OpenAPI, health checks, аудит безопасности.
- SMTP-отправка через transactional outbox, Docker, тесты, README.

### 1.3. Строго вне объёма версии 1.0

- Бизнес-роли, разрешения, организации, tenants, устройства и другие доменные сущности внешних приложений.
- Собственная реализация OAuth 2.0, OpenID Connect, SSO, authorization code flow, PKCE и OIDC discovery.
- Социальный вход, MFA/TOTP, passkeys, SAML, LDAP.
- Административный кабинет, frontend и самостоятельный браузерный SPA-клиент.
- Password grant, `client_credentials` и произвольные OAuth endpoints — **не объявлять API реализацией OAuth/OIDC**.
- Сложная событийная архитектура, брокер сообщений, Kubernetes, Redis и распределённые БД.

### 1.4. Сценарии клиентов

- **Native app / доверенный backend / BFF:** вызывает REST API; refresh token возвращается в JSON. Native сохраняет его только в защищённом хранилище ОС. Backend/BFF — только в защищённом серверном хранилище.
- **Браузерный frontend:** работает через BFF внешнего приложения, который управляет защищённой HttpOnly-cookie-сессией. Не класть refresh token в `localStorage`, `sessionStorage` или доступный JavaScript cookie.
- `clientId` является публичным идентификатором и **не** является доказательством подлинности клиента. Отсутствие OAuth/OIDC означает, что сервис **не предоставляет стандартизованный SSO для произвольных сторонних SPA**. Это явно задокументировать.

## 2. Технологический стек

- .NET **10 LTS**, C# актуальной стабильной версии для .NET 10.
- ASP.NET Core MVC Controllers, `[ApiController]`, attribute routing, `AddControllers()`, `MapControllers()`.
- EF Core 10 + Npgsql, PostgreSQL 17+ (поддерживать совместимые версии Npgsql/PostgreSQL).
- CQRS и DI: собственные интерфейсы + стандартный `Microsoft.Extensions.DependencyInjection`, без MediatR и стороннего mediator framework.
- Пароли: `Microsoft.AspNetCore.Identity.PasswordHasher<T>` только как криптографический примитив PBKDF2, **без** Identity `UserManager`, `RoleManager`, `IdentityDbContext`, Identity endpoints и Identity UI. Установить `IterationCount = 600_000` как стартовую политику; проверить латентность на целевой машине, вынести настройку в конфигурацию. Хеши должны иметь версию/параметры и возможность rehash.
- JWT: стандартная библиотека Microsoft IdentityModel/JwtBearer; асимметричная подпись **RS256**, ключ RSA не менее 2048 бит (предпочтительно 3072 бит).
- Криптографическая случайность: `RandomNumberGenerator`.
- Отправка писем: SMTP через проверенную библиотеку (например, MailKit); локальная разработка с Mailpit.
- Логи: `ILogger<T>` и стандартные средства ASP.NET Core; структурированные события, без секретов.
- Тесты: xUnit, ASP.NET Core `WebApplicationFactory`, `Testcontainers.PostgreSql`, без обязательных платных библиотек.
- Контейнеризация: многостадийный Dockerfile + `docker-compose.yml` для локальной разработки.
- Управление версиями пакетов — `Directory.Packages.props` (Central Package Management). Не добавлять библиотеку без конкретной необходимости.

## 3. Clean Architecture и состав solution

```text
Identity.slnx
src/
  Identity.Domain/
    Entities/              User, Credential, Session, RefreshToken, OneTimeToken
    Enums/                 UserStatus, OneTimeTokenPurpose, SessionRevokeReason
    Exceptions/
  Identity.Application/
    Abstractions/
      Cqrs/                ICommand, IQuery, Handlers, Dispatchers, Unit, Validators
      Persistence/         repositories, IUnitOfWork
      Security/            IPasswordService, ITokenService, ITokenGenerator
      Notifications/       IEmailMessageFactory (при необходимости)
    Features/
      Auth/Register/
      Auth/VerifyEmail/
      Auth/RequestEmailVerification/
      Auth/Login/
      Auth/Refresh/
      Auth/Logout/
      Auth/LogoutAll/
      Auth/RequestPasswordReset/
      Auth/ResetPassword/
      Users/GetCurrentUser/
      Users/UpdateProfile/
      Users/ChangePassword/
      Users/DeactivateAccount/
      Sessions/GetSessions/
      Sessions/RevokeSession/
    Common/               Result/ошибки, clock abstractions при необходимости
    DependencyInjection.cs
  Identity.Infrastructure/
    Persistence/
      IdentityDbContext.cs
      Configurations/
      Repositories/
      Migrations/
    Security/              PasswordService, JwtTokenService, RefreshTokenGenerator, JwksProvider
    Email/                 SMTP sender, outbox worker, templates
    DependencyInjection.cs
  Identity.Api/
    Controllers/           AuthController, UsersController, SessionsController, DiscoveryController
    Contracts/             Request/Response DTO
    Dispatching/           CommandDispatcher, QueryDispatcher
    Middleware/            исключения, корреляция
    Configuration/
    Program.cs
tests/
  Identity.UnitTests/
  Identity.IntegrationTests/
  Identity.ApiTests/
docs/
  architecture.md
  security.md
  api.md
  deployment.md
Dockerfile
compose.yaml
compose.override.yaml         # опционально для dev
.env.example
.editorconfig
Directory.Build.props
Directory.Packages.props
README.md
```

**Зависимости:** `Domain` не зависит ни от чего; `Application -> Domain`; `Infrastructure -> Application, Domain`; `Api -> Application, Infrastructure` (Api — composition root). Tests могут ссылаться на нужные проекты. Запретить обратные ссылки. В Domain/Application не использовать `DbContext`, `HttpContext`, `ControllerBase`, `IConfiguration`, JWT/SMTP/PostgreSQL типы.

Организация `Application` — по функциональным вертикальным срезам, отдельные типы команды, результата, обработчика, валидатора. Контроллеры тонкие; бизнес-логика только в Application/Domain; детали хранения — Infrastructure.

## 4. CQRS: собственный минимальный контракт

Определить типобезопасные контракты (пример формы API, имена допускается уточнить без потери семантики):

```csharp
public interface ICommand<TResult> { }
public interface IQuery<TResult> { }
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken);
}
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}
public interface ICommandDispatcher
{
    Task<TResult> Execute<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand<TResult>;
}
public interface IQueryDispatcher
{
    Task<TResult> Execute<TQuery, TResult>(TQuery query, CancellationToken ct = default)
        where TQuery : IQuery<TResult>;
}
public readonly record struct Unit;
```

- Диспетчеры в `Identity.Api/Dispatching`, разрешают конкретный handler через `GetRequiredService<ICommandHandler<TCommand,TResult>>()` / аналог для запросов; никаких `dynamic`, `MakeGenericType` в hot path и выполнения через reflection.
- `ICommandDispatcher` и `IQueryDispatcher` — `Scoped`. Один обработчик на пару `(TRequest,TResult)`; явно регистрировать обработчики в `AddApplication()` без assembly scanning. Проверять DI graph на старте/test host.
- Добавить `ICommandValidator<TCommand>` / `IQueryValidator<TQuery>` с типизированными ошибками полей. Общая валидация вызывается диспетчером перед обработчиком, плюс доменные инварианты в Domain. Валидация не должна зависеть от `HttpContext`.
- Обработчики не вызывают напрямую другие обработчики. Вынесение общей логики — сервис Application или доменный метод.
- Команды меняют состояние; запросы не меняют его. `Login`, `Refresh`, `Logout` — команды, потому что они меняют сессии.
- **Не** создавать generic repository, универсальную UnitOfWork-иерархию и систему notifications/behaviors, пока нет реального кейса.
- Все IO операции асинхронны и принимают `CancellationToken`; время берётся через внедрённый `TimeProvider`.
- Транзакции явные на границах use case. Несколько записей одного `DbContext.SaveChangesAsync` атомарны, но rotation и отзыв семейства токенов требуют строгой конкурентной защиты.

## 5. Предметная модель и PostgreSQL

Использовать UUID (`Guid`) как PK, `timestamptz`/`DateTimeOffset` UTC для времени. Имена таблиц в `snake_case`. EF Core Fluent API и миграции, **без** `EnsureCreated` в production. Пароли, refresh tokens и одноразовые токены в открытом виде в БД запрещены.

### 5.1. `users`

| Поле | Тип | Требования |
|---|---|---|
| `id` | uuid PK | генерируется приложением |
| `email` | varchar(320) | исходное нормализованное отображаемое значение, не пустое |
| `normalized_email` | varchar(320) | нормализация по единой функции; unique индекс |
| `display_name` | varchar(120) nullable | пользовательский профиль |
| `status` | smallint | PendingVerification / Active / Disabled / Deactivated |
| `email_verified_at` | timestamptz nullable | выставляется только после валидного токена |
| `created_at`, `updated_at` | timestamptz | UTC |
| `deactivated_at` | timestamptz nullable | признак деактивации |
| `failed_login_count`, `locked_until` | int, timestamptz nullable | защита от перебора; обновление конкурентно-безопасно |

Нормализация email: один централизованный алгоритм (`Trim`, нормализация регистра invariant, без специальных Gmail-правил); исходный email хранить для отображения. Пароль не `Trim` и не менять молча; политику Unicode-нормализации пароля задокументировать и применять одинаково при hash/verify (NFKC при наличии поддержки). Email validation не должна принимать CR/LF/control chars.

### 5.2. `credentials`

`user_id uuid PK/FK users`, `password_hash text NOT NULL`, `created_at`, `password_changed_at`. Отделить хеш от профиля. Уникальная связь один-к-одному.

### 5.3. `sessions`

`id uuid PK`, `user_id uuid FK`, `client_id varchar(100)`, `created_at`, `last_used_at`, `absolute_expires_at`, `revoked_at`, `revoke_reason`, `user_agent` (обрезанный/санитизированный, необязательный). Индекс `(user_id, revoked_at)`. Доступ к чужим сессиям запрещён. Сессия — семейство связанных refresh tokens.

### 5.4. `refresh_tokens`

`id uuid PK`, `session_id uuid FK`, `token_hash char(64) UNIQUE`, `created_at`, `expires_at`, `consumed_at`, `revoked_at`, `replaced_by_token_id uuid NULL`. Хранить **историю** использованных токенов до безопасного срока очистки: это необходимо для обнаружения replay. Индексы по `session_id`, `expires_at`.

### 5.5. `one_time_tokens`

`id uuid PK`, `user_id uuid FK`, `purpose smallint` (EmailVerification, PasswordReset), `token_hash char(64) UNIQUE`, `created_at`, `expires_at`, `consumed_at`. Сроки: verification 24 часа; password reset 30 минут. Только single-use. Применять в той же транзакции, что и изменение подтверждения/пароля. При повторной выдаче допустимо отзывать предыдущие активные токены того же назначения.

### 5.6. `outbox_messages`

`id uuid PK`, `type varchar(80)`, `payload jsonb`, `created_at`, `next_attempt_at`, `attempt_count`, `processed_at`, `last_error` (без секретов). Используется только для надёжной доставки email. **Важный нюанс:** в outbox нельзя хранить одноразовый токен или ссылку с токеном открытым текстом, хотя worker должен получить исходное значение для письма. Шифровать чувствительную часть payload аутентифицированным шифрованием (AES-256-GCM через системный crypto API) отдельным ключом, который хранится вне БД и репозитория; включить key ID и возможность ротации. В `one_time_tokens` хранить только hash. После успешной доставки очищать ciphertext/payload по политике retention. Создание one-time token + шифрованной outbox записи — в одной транзакции. Worker в том же процессе API: claim записей с `FOR UPDATE SKIP LOCKED` (или эквивалент), ограниченные ретраи с backoff, защита от параллельной обработки несколькими инстансами, метрика недоставленных писем. SMTP допускает повторную доставку; шаблоны/потребители должны это учитывать.

### 5.7. `security_audit_events`

`id uuid PK`, `user_id uuid nullable`, `event_type`, `occurred_at`, `client_id nullable`, `correlation_id`, `metadata jsonb` (только whitelisted несекретные поля). События: register, verify_email, login_success, login_failed, refresh_rotated, refresh_replay_detected, logout, logout_all, password_changed, password_reset, account_deactivated. Настраиваемое время хранения (по умолчанию 90 дней); хранение IP/PII минимизировать и документировать.

### 5.8. Миграции и ограничения

- Все FK, check constraints и уникальные индексы создавать миграциями.
- `normalized_email` и `token_hash` должны иметь уникальные индексы в **БД**, не только application-level проверки.
- Все переходы состояния токенов и проверку конкурентных запросов выполнять атомарно. Для refresh использовать row-level lock в PostgreSQL на сессию/токен либо эквивалентную CAS/optimistic concurrency со строгими тестами.
- Миграции запускать отдельной deployment-командой, не автоматически на каждом старте production API.

## 6. Аутентификация и безопасность токенов

### 6.1. JWT access token

- Ассиметрично подписанный JWT RS256, RSA private PEM через секретный mount / secret provider, публичный ключ для внешних API. Секреты и ключи не генерировать автоматически при каждом старте production.
- `accessTokenLifetime = 15 minutes`, допустимый clock skew <= 30 seconds. Конфигурируемые пределы и проверки options на старте.
- Claims: `sub` (user UUID), `iss` (фиксированный HTTPS issuer), `aud` (разрешённая audience из client configuration), `jti` (случайный идентификатор), `sid` (session UUID), `client_id`, `iat`, `nbf`, `exp`, `token_use=access`. Не включать пароль, refresh token, бизнес-роли или избыточную PII.
- Header: `alg=RS256`, `kid=<active-key-id>`; подпись и `kid` проверяются строго, `alg=none` и неподдерживаемые алгоритмы отклоняются.
- Discovery: `GET /.well-known/jwks.json` возвращает только публичные JWK (`kty`, `use`, `alg`, `kid`, `n`, `e`), поддерживает плановую ротацию ключей и публикацию старого public key до истечения выданных им токенов.
- Серверы-потребители должны проверять `iss`, **свою** `aud`, подпись, `exp`, `nbf`, тип токена. Не принимать токены другого audience.
- Identity на собственных `[Authorize]` endpoints валидирует JWT, разрешённую аудиторию, `token_use=access`, и проверяет активность пользователя/сессии по БД. Это не требует обращения к Identity со стороны других API при каждом запросе.
- Отзыв refresh-сессии **не** отзывает автоматически ранее выданный JWT в стороннем API; он действует максимум до своего `exp`. Указать это в security docs.

### 6.2. Client configuration

- Клиенты определяются конфигурацией, например `Identity:Clients` с `ClientId`, `Audience`, `Enabled`, без админ-CRUD в v1.
- На login клиент передаёт `clientId`; неизвестный/отключённый клиент отклоняется. Для пользователя это **не** полноценная аутентификация клиента; `clientId` — публичный селектор audience.
- На refresh нельзя сменить исходный `clientId` или audience: значения привязаны к сессии.
- Одновременно можно настроить несколько клиентов; бизнес-ролей и привязок пользователя к клиентам нет.

### 6.3. Refresh tokens

- Непрозрачная строка base64url из **32 или более криптографически случайных байт**. БД хранит только SHA-256 хеш (допустим из-за высокой энтропии случайного токена).
- Срок жизни refresh-токена до 7 дней; максимальная абсолютная жизнь сессии 30 дней. Новый refresh token не должен продлевать сессию дальше `absolute_expires_at`.
- При refresh текущий token атомарно помечается consumed, создаётся новый в том же семействе, выдаётся новая пара access/refresh.
- При повторном предъявлении **ранее использованного** токена — записать audit event, отозвать **всю** сессию/семейство; все последующие refresh этой сессии отклонять.
- Если refresh просрочен, сессия/пользователь заблокированы, аудитория не совпадает, токен неизвестен или отозван — ответ без выдачи токенов.
- Согласованность при параллельных запросах обязательна; тестировать конкурентный refresh. Клиентам документировать правило сериализации refresh для одной сессии.
- Logout текущей сессии по refresh token; logout-all и точечный revoke по авторизованному пользователю.
- При password reset, смене пароля и деактивации отозвать все сессии (для смены пароля допустимо оставить текущую только по явно выбранной политике; в v1 отзывать **все**, включая текущую).

### 6.4. Пароли

- Минимальная длина **15 Unicode символов** для однофакторного входа, максимум 128 символов, защита от чрезмерного размера входа по байтам. Разрешить пробелы и Unicode; не требовать прописную, строчную, цифры или спецсимволы по фиксированным правилам.
- Блокировать распространённые и заведомо скомпрометированные пароли по локальному denylist (минимум распространённые пароли); не передавать пароль внешним сервисам. При необходимости отдельно расширить проверкой password breach без раскрытия пароля.
- `PasswordHasher<T>` c PBKDF2 и настроенной сложностью; при `SuccessRehashNeeded` обновлять хеш при успешном login.
- Generic login response для неизвестного пользователя, неверного пароля, непроверенного email или заблокированной учётной записи. Не раскрывать существование email через login/reset/verification-request.
- Rate limiting: по IP и account identifier, счётчик/временная блокировка с конкурентно-безопасным обновлением. Настраиваемые лимиты, логирование без email в общих метриках.

### 6.5. Общая безопасность

- Только HTTPS в production, HSTS за корректно настроенным reverse proxy. CORS по умолчанию **выключен**; никаких `AllowAnyOrigin` + credentials.
- JSON limits, ограничение размера запросов, валидация полей, ошибки без stack trace и без сырых исключений БД.
- Скрыть любые токены, пароли, SMTP credentials, private key из HTTP/access/audit логов и трассировок. Не передавать token в query API; одноразовые токены только в JSON body. Ссылки из email формирует только фиксированный server-side template, без произвольного redirect от клиента. Предпочтительно передавать token на web page через fragment URL, если внешняя UI умеет это безопасно обработать.
- Перед изменением email в будущем потребуются повторная верификация и защита от takeover; **в v1 email неизменяемый**.
- Не делать `DELETE /users/me` как ложное обещание немедленного уничтожения PII; реализовать прозрачную деактивацию и задокументировать политику последующего удаления данных.

## 7. HTTP API и контракты

Базовый route: `/api/v1`. Только MVC Controllers, JSON `camelCase`, единая структура ошибок `ProblemDetails` (RFC 9457) с `code`, `traceId`, `errors` для validation. В контроллерах использовать DTO, **не** Domain Entity. API versioning путём route (`v1`). Использовать `[Authorize]`, `[AllowAnonymous]` осознанно; по умолчанию endpoints закрыты, кроме специально перечисленных. Контроллеры не получают `DbContext` и репозитории.

| Метод | Endpoint | Доступ | Семантика и успешный ответ |
|---|---|---|---|
| POST | `/api/v1/auth/register` | public | `{email,password,displayName?,clientId}` -> **202** generic, независимо от существования email; создаёт PendingVerification + письмо |
| POST | `/api/v1/auth/email/verify` | public | `{token}` -> **204**, включает Active и фиксирует verifiedAt |
| POST | `/api/v1/auth/email/verification-request` | public | `{email}` -> **202** generic |
| POST | `/api/v1/auth/login` | public | `{email,password,clientId}` -> **200** токены; **401** generic при отказе |
| POST | `/api/v1/auth/refresh` | public | `{refreshToken}` -> **200** новая пара; **401** при отказе |
| POST | `/api/v1/auth/logout` | public | `{refreshToken}` -> **204**, идемпотентное завершение сессии без раскрытия существования токена |
| POST | `/api/v1/auth/logout-all` | Bearer | **204**, все сессии пользователя отозваны |
| POST | `/api/v1/auth/password/forgot` | public | `{email}` -> **202** generic |
| POST | `/api/v1/auth/password/reset` | public | `{token,newPassword}` -> **204**, отзыв всех сессий |
| POST | `/api/v1/auth/password/change` | Bearer | `{currentPassword,newPassword}` -> **204**, отзыв всех сессий |
| GET | `/api/v1/users/me` | Bearer | **200** профиль |
| PATCH | `/api/v1/users/me` | Bearer | `{displayName}` -> **200** обновлённый профиль; email не изменяется |
| POST | `/api/v1/users/me/deactivate` | Bearer | `{password}` -> **204**, деактивация и отзыв всех сессий |
| GET | `/api/v1/sessions` | Bearer | **200**, активные сессии текущего пользователя; `isCurrent` по `sid` |
| DELETE | `/api/v1/sessions/{id:guid}` | Bearer | **204** для своей сессии, **404** для чужой/отсутствующей |
| GET | `/.well-known/jwks.json` | public | **200**, публичные ключи (JWK Set), без приватных компонентов |
| GET | `/health/live` | public/internal | **200**, процесс жив |
| GET | `/health/ready` | internal | **200/503**, готовность подключения к БД |

У `/health/ready` ограничить доступ на сетевом уровне; не выдавать connection strings, SMTP credentials или детали БД.

**Token response:**

```json
{
  "accessToken": "eyJ...",
  "refreshToken": "opaque-base64url-value",
  "tokenType": "Bearer",
  "expiresIn": 900
}
```

**Profile response:**

```json
{
  "id": "72e11cbd-4a67-4f72-b1c2-af2439798ae7",
  "email": "user@example.com",
  "displayName": "Example User",
  "emailVerified": true,
  "status": "active",
  "createdAt": "2026-10-19T10:00:00Z"
}
```

**ProblemDetails response (пример):**

```json
{
  "type": "about:blank",
  "title": "Validation failed",
  "status": 400,
  "code": "validation_failed",
  "traceId": "00-...",
  "errors": { "email": ["Invalid email format"] }
}
```

Статусы: `400` validation, `401` invalid credentials/token, `403` forbidden, `404` absent/foreign resource, `409` только для несекретных конфликтов, `429` rate limit (с `Retry-After` по возможности), `500` generic. Не выдавать `409 email already exists` при public registration. Для недействительного/использованного one-time token отдавать одинаковую ошибку без различения причины. Нормализовать внешние ошибки в константные machine-readable коды.

Отдельно документировать примеры `curl` и Bearer для сторонних API, свойства issuer/audience/JWKS и тайм-ауты. OpenAPI JSON — как минимум в development, документация public API — в репозитории.

## 8. Почтовая отправка и фоновые задачи

- SMTP через `IEmailSender`, реализация в Infrastructure; конфигурация host, port, auth, TLS, from address через env/secrets.
- Dev: Mailpit в Docker Compose. Prod: не запускаться с включённой публичной регистрацией при отсутствии корректной конфигурации доставки email/verification templates и ключа шифрования чувствительного outbox payload.
- EmailVerification и PasswordReset: нейтральные шаблоны на английском, бренд **Identity**, не указывать имена внешних продуктов.
- Письмо строится по server-side конфигурации `Email:VerificationUrlTemplate` и `Email:ResetPasswordUrlTemplate`, где только placeholder `{token}`. Redirect URL из клиентского запроса не принимать. URL HTTPS в production; local HTTP допустим только development.
- One-time token создаётся транзакционно с outbox. Worker повторяет отправку при временных ошибках с exponential backoff, ограничением попыток и операторским сигналом о dead-letter/failure.
- Очистка просроченных токенов, обработанных outbox и audit events — через `BackgroundService` c ограниченными batch sizes. Срок хранения должен оставлять использованные refresh tokens достаточно долго для обнаружения replay до истечения срока соответствующей сессии.

## 9. Нефункциональные требования

**Надёжность:** нет in-memory состояния, критичного для правильности сессий; restart контейнера не инвалидирует токены, ключи не пересоздаются. Одно приложение API + PostgreSQL, без обязательных Redis/RabbitMQ. Готовность к нескольким экземплярам достигается транзакционными блокировками и устойчивой обработкой outbox.

**Производительность:** типичный API запрос не должен вызывать лишние SQL; все query handlers используют `AsNoTracking` для read-only операций; JWT в сторонних API проверяется локально без обращения к Identity. В login ожидается более высокая задержка из-за защищённого password hash; не снижать безопасность ради произвольного SLA. Добавить минимальные нагрузочные измерения login и refresh.

**Конфигурация:** все настройки через options pattern + `ValidateOnStart`. Пример env variables, отдельно dev/prod. Не хранить секреты в appsettings и Git. Outbox encryption key, приватный ключ JWT и SMTP пароль — отдельные секреты; ключи шифрования не удалять до отправки/очистки зашифрованных сообщений. `ASPNETCORE_ENVIRONMENT=Production` не допускает тестовых ключей и незащищённых SMTP-настроек.

**Наблюдаемость:** structured logs, correlation/trace ID, метрики успешных/неуспешных попыток, replay, active sessions и backlog outbox; без паролей, токенов и сырой PII в логах или metric labels.

**Тестируемость:** внедряемый `TimeProvider`, детерминированные unit tests на сроки действия; in-memory provider EF Core не используется вместо PostgreSQL в интеграционных тестах.

**Документация:** README для быстрого запуска, `docs/architecture.md` (зависимости), `docs/security.md` (threat model и ограничения), `docs/api.md`, `docs/deployment.md` (TLS, keys, backups, migrations, maintenance).

## 10. План реализации — выполнять строго по этапам

### Этап 0. Solution и каркас архитектуры

**Сделать:**
- Создать solution и четыре проекта из раздела 3; три test projects.
- Настроить project references, Directory.Build.props, Central Package Management, nullable, warnings-as-errors для собственного кода, format/editorconfig.
- ASP.NET Core **Controllers** (`AddControllers`, `MapControllers`), `ApiController`, API route version v1, OpenAPI dev, ProblemDetails, health/live.
- Infrastructure/Application `AddInfrastructure`, `AddApplication` extension methods.
- Dockerfile + Compose: API, PostgreSQL, Mailpit; `.env.example`, `README.md` (bootstrap).
- Простой smoke test HTTP и CI команды `dotnet restore`, `dotnet build`, `dotnet test`.

**Готово, когда:** solution собирается; API стартует; `/health/live` отвечает; нет ни одного domain-specific имени; нет Minimal API бизнес-endpoints; тесты работают.

### Этап 1. Domain, EF Core и собственный CQRS

**Сделать:**
- Domain entities/state transitions по разделу 5 без public setters.
- CQRS interfaces, `Unit`, dispatcher, validators, явная регистрация обработчиков; демонстрационный реальный query/handler внутри архитектуры (без искусственного production endpoint).
- DbContext, entity configurations, repositories, UnitOfWork; initial migrations всех таблиц раздела 5.
- Security abstractions; `TimeProvider`; DI validation; global exception handling и error codes.
- Integration tests на индексы, FK, UTC, транзакции и архитектурные зависимости.

**Готово, когда:** реальные миграции применяются на Testcontainers PostgreSQL; CRUD хранилища покрыт тестами; CQRS handler вызывается строго один раз; validators срабатывают до handler; Domain/Application не зависят от ASP.NET/EF.

### Этап 2. Register + email verification

**Сделать:**
- RegisterCommand, валидатор и controller action.
- Безопасный password hashing/rehashing contract, политика паролей, уникальность email, конкурентные регистрации.
- Создание `PendingVerification`, verification token, outbox event в одной транзакции.
- SMTP sender, Mailpit, worker, `verify` и `verification-request`.
- Generic registration response и защита от user enumeration; начальный rate limit.
- Unit/API/integration tests нормального пути, дубля email, SMTP failure, single-use, expired token, race.

**Готово, когда:** письмо приходит в Mailpit; после подтверждения пользователь становится Active; повторное подтверждение невозможно; дубль email не создаёт вторую запись; сбой SMTP не теряет outbox запись.

### Этап 3. Login, JWT и JWKS

**Сделать:**
- LoginCommand, валидатор, проверка email/password/status, политика временной блокировки.
- Client config + issuer/audience; RS256 provider; ключи из файлов/secret mount, startup validation; JWT Bearer validation для защищённых Identity endpoints.
- JWKS publishing endpoint с `kid`, только public key, документация key rotation.
- Создание Session + initial RefreshToken после успешного login.
- Audit events; защита от слишком частых login попыток по IP/account.
- Integration tests: корректная подпись, неправильный issuer/audience/alg, disabled user, wrong password, rate limit, key restart persistence.

**Готово, когда:** `login` выдаёт проверяемый JWT и refresh token; стороннее тестовое приложение может проверить JWT через JWKS без приватного ключа; неподходящая audience отклоняется; нет секретов в логах.

### Этап 4. Refresh rotation, Logout, Sessions

**Сделать:**
- RefreshCommand со строгой транзакцией/блокировками, consumed/replaced chain, replay detection и отзывом всего семейства.
- Logout, LogoutAll, GetSessions, RevokeSession, проверка собственника.
- Cleanup использованных/просроченных токенов в допустимых границах удержания истории.
- Тесты на последовательные refresh, 2+ конкурентных refresh одного токена, replay старого токена, истечение срока, logout/logout-all, отзыв чужой сессии, перезапуск процесса.

**Готово, когда:** ни один токен не может быть успешно ротирован дважды; replay приводит к отзыву session; чужую сессию нельзя просмотреть/отозвать; данные после restart консистентны.

### Этап 5. Profile и password lifecycle

**Сделать:**
- GetCurrentUser, UpdateProfile (только displayName), ChangePassword.
- RequestPasswordReset + ResetPassword: одноразовые short-lived tokens, generic request response, transactional outbox, atomic consume.
- DeactivateAccount с проверкой текущего пароля и отзывом всех сессий; определить ответ при дальнейших login.
- Revoke-all sessions при смене/сбросе пароля; audit.
- Проверки прав только для своей учётной записи; защититься от IDOR.

**Готово, когда:** пользователь не может получить/изменить чужой профиль; reset token одноразовый и истекающий; старый пароль не подходит; все сессии после password reset/change/deactivation отозваны.

### Этап 6. Security hardening и observability

**Сделать:**
- Единая таксономия ProblemDetails, корректные 400/401/403/404/429/500; ограничения body и длины строк.
- Аудит и безопасные structured logs; correlation ID; метрики auth и backlog email без PII.
- Проверить HTTPS/proxy headers, CORS default deny, безопасные error responses, секреты в конфигурации, production startup validation.
- Test suite негативных кейсов: enumeration/timing paths, oversized input, brute force, token tampering, invalid signature, expiry, revoked account/session, refresh token replay, concurrent operations.
- Сделать security checklist в docs с известными ограничениями (без OAuth/SSO/MFA, JWT revocation lag).

**Готово, когда:** все критические негативные сценарии покрыты API/integration tests; audit без секретов; конфигурация production с тестовыми ключами не стартует.

### Этап 7. Поставка как самостоятельного мини-продукта

**Сделать:**
- Финальный Dockerfile non-root, healthchecks, Compose для local dev, отдельно production deployment guidance.
- README: архитектура, требования, быстрый запуск, EF migrations, SMTP/Mailpit, keys, env variables.
- `docs/api.md` с curl для всех операций; пример проверки JWT из другого .NET API через `Authority`/JWKS или явно указанный issuer/key resolver + правильная audience validation. **Не** выставлять несуществующий OIDC `Authority` как будто сервис публикует OIDC discovery; показать реальную ручную настройку JWKS.
- Конфигурация CI: restore/build/test, optional container build, форматирование, миграции в test DB; проверка отсутствия committed secrets.
- Выполнить clean build/test, end-to-end smoke: register -> Mailpit -> verify -> login -> refresh -> me -> logout -> failed refresh.
- Подготовить `CHANGELOG.md`/release notes `1.0.0` и список ограничений.

**Готово, когда:** человек, не знакомый с проектом, может поднять его локально по README и пройти весь сценарий; сборка/тесты зелёные; production-инструкция не требует изменения исходного кода.

## 11. Обязательная матрица тестов

**Unit tests**:
- Нормализация email; доменные инварианты; переходы статусов.
- Пароль: длина, Unicode, пробелы, denylist, хеширование и проверка, rehash.
- CQRS dispatcher вызывает нужный handler; валидаторы блокируют выполнение; CancellationToken прокидывается.
- Проверка сроков действия через fake TimeProvider.
- Handler cases: корректный результат, invalid state, конфликт и отсутствие пользователя.

**Integration/API tests (реальный PostgreSQL)**:
- Уникальность email при конкуренции (две параллельные регистрации).
- Создание пользователя/credentials/outbox атомарно.
- Подтверждение email и password reset one-time/expiration; generic responses.
- Успешный/неуспешный login, блокировка, пароль после смены.
- Подпись JWT, JWKS, `kid`, `iss`, `aud`, `exp`, `nbf`, отклонение tampered tokens.
- Refresh rotation: успешная последовательность, replay, два одновременных запроса; ошибка до/после коммита без нарушения целостности.
- Logout, logout-all, unauthorized/foreign session access.
- Невозможность доступа с disabled/deactivated user на Identity endpoints; объяснённая задержка отзыва токена в downstream.
- Outbox retries, Mailpit, отсутствие отправки или дублей при конкуренции workers в разумных границах at-least-once.
- Ограничение размеров и rate limiting; `traceId` и отсутствие stacktrace/secrets в ошибках.

## 12. Стандарты качества кода и запреты

- Развёрнутые имена типов и методов, небольшой контроллер, один use case на handler.
- Запрещены MediatR, готовые auth servers, ASP.NET Identity `UserManager`/`SignInManager`/Identity EF tables, Minimal API для бизнес-операций, generic repository и внедрение DbContext в контроллер.
- Никаких хардкоженных паролей, приватных ключей, test API keys или CORS `*` в production.
- Никогда не логировать raw password, token, URL с одноразовым токеном, SMTP secret.
- Не создавать новый access token при logout; не заменять rotation простым увеличением срока JWT.
- Соблюдать nullable, cancellation, async, idempotency там, где оговорено.
- **Не** вводить роли/permissions в Identity и не добавлять доменные атрибуты сторонних систем.
- Строго документировать ограничение: собственный REST token protocol не является OAuth2/OIDC и не должен использоваться вместо стандартизованного федеративного входа там, где он требуется.

## 13. Формат отчёта Codex после каждого этапа

1. **Этап:** номер и название.
2. **Реализовано:** функциональность и ключевые файлы.
3. **Решения:** важные технические компромиссы; ссылка на спецификацию, если поведение уточнено.
4. **Проверки:** точные запущенные команды и результаты (build, test, migrations, docker).
5. **Безопасность:** какие угрозы закрыты на этом этапе, какие остаются до следующих этапов.
6. **Ограничения/блокеры:** без сокрытия недоделанной функциональности.
7. **Следующий этап:** только рекомендация, **не** выполнение.

## 14. Готовые команды для последовательной работы с Codex

**Первый запуск:**

> Прочитай `Identity_Codex_Spec.md` целиком. Реализуй только **Этап 0** по спецификации. Не делай работу следующих этапов. Сначала кратко представь структуру, затем внеси файлы, запусти сборку и тесты. После выполнения отчитайся строго по разделу 13 и остановись.

**Каждый следующий запуск:**

> Прочитай `Identity_Codex_Spec.md`, текущую реализацию и результаты предыдущего этапа. Реализуй только **Этап N** со всеми перечисленными проверками и критериями. Исправляй дефекты предыдущих этапов, которые блокируют этот этап, но не внедряй функции следующих этапов. В конце проверь build/tests, обнови документацию и отчитайся по разделу 13.

**Финальная проверка:**

> Проведи ревью готового Identity по `Identity_Codex_Spec.md`, составь матрицу покрытия требований, найди нарушения Clean Architecture, уязвимости и сценарии гонок. Исправь найденные ошибки, выполни полный набор тестов и покажи остаточные риски. Не добавляй функции вне v1.0.

## 15. Проверочные источники и нормативные ориентиры

- Официальная политика поддержки .NET: https://dotnet.microsoft.com/en-us/platform/support/policy
- OWASP Password Storage: https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html
- OWASP Authentication: https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html
- OWASP Forgot Password: https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html
- NIST SP 800-63B-4: https://pages.nist.gov/800-63-4/sp800-63b.html
- RFC 9700 (OAuth 2.0 Security BCP — требования к rotation и replay применимы как ориентир безопасности, без заявления о поддержке OAuth): https://www.rfc-editor.org/rfc/rfc9700.html
- RFC 8725 (JWT BCP): https://www.rfc-editor.org/rfc/rfc8725.html
- ASP.NET Core OpenAPI: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0

**Конец ТЗ.**
