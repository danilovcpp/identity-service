# Identity Admin Console — дополнение к Identity_Codex_Spec.md

**Версия:** v1.1 (планируемая)  
**Статус:** обязательное техническое задание для реализации административного веб-интерфейса  
**Назначение:** самостоятельный продукт `Identity`, без какой-либо зависимости от предметной области внешних приложений.

## 0. Приоритеты и совместимость с базовым ТЗ

1. Перед началом прочитать `AGENTS.md` и `Identity_Codex_Spec.md`. Это дополнение **расширяет** спецификацию, не заменяет требования безопасности, Clean Architecture, Controllers, собственный CQRS и тестирование.
2. В базовой спецификации административный кабинет был *вне scope v1.0*. Это дополнение добавляет его в релиз **v1.1**. Противоречия разрешать в пользу этого документа **только в отношении admin функциональности**; остальные требования основной спецификации сохраняются.
3. Не использовать MediatR, AspNetCore Identity UI/`UserManager`/`RoleManager`, готовые IAM-серверы, React Admin или неоговорённые генераторы CRUD.
4. Вся авторизация и проверка прав выполняются на backend. Скрытие элементов UI — только удобство, не мера безопасности.
5. Избегать избыточных абстракций. Каждая команда и запрос — отдельный вертикальный срез. БД одна (PostgreSQL), публичные API не ломать.
6. Никаких названий и сущностей, относящихся к клиентским приложениям; Identity должен оставаться переиспользуемым.

## 1. Цель и функциональная область

Реализовать `Identity.Admin.Web` — веб-консоль администратора с отдельным административным API внутри существующего `Identity.Api`.

**Основные сценарии:**

- Dashboard: количество пользователей всего, активных, ожидающих подтверждения, заблокированных, деактивированных; активные сессии; регистрации по дням за 30 дней. Отдельно показывать период, по которому считается метрика.
- Список пользователей с серверной пагинацией, полнотекстоподобным поиском по email/display name (для MVP `ILIKE` при контроле размеров), фильтрацией по status/emailVerified и сортировкой по whitelisted полям.
- Карточка пользователя: основные свойства, состояние email, дата регистрации, доступные сессии, последние события безопасности; поля с паролями, хешами и токенами никогда не отдавать.
- Создание пользователя администратором **по email-приглашению** с безопасной первичной установкой пароля; пароль не назначается и не показывается администратору.
- Повторная отправка приглашения.
- Инициирование сброса пароля пользователя через подтверждённый почтовый канал; без генерации пароля администратором.
- Ручная блокировка/разблокировка пользователя, с причиной блокировки; отзыв его refresh-сессий при блокировке.
- Просмотр и отзыв активных сессий пользователя.
- Список административных действий (audit log), фильтры по пользователю, событию, времени и администратору.

**Вне MVP:** редактирование email пользователю без re-verification, просмотр паролей, выдача reset-токенов администратору, impersonation, произвольное редактирование JWT claims, управление ролями внешних приложений, multi-tenancy, bulk-delete, массовые рассылки, отдельный Identity Provider с OAuth/OIDC, полноценное управление администраторами через UI.

## 2. Технологический стек

- React 19 + TypeScript (`strict: true`), Vite.
- UI: `antd` **6** и совместимая `@ant-design/icons` **6**; не добавлять другую компонентную библиотеку.
- React Router (совместимый с выбранными React/Vite версиями).
- TanStack Query для данных, мутаций, обновления кеша и loading/error состояний.
- Fetch и один централизованный типизированный API-клиент; по возможности типы генерировать по OpenAPI (`openapi-typescript`), а не дублировать вручную.
- Ant Design `Form`, `Table`, `Layout`, `Modal`, `Drawer`, `Statistic`, `Tag`, `Message`, `Notification`; Recharts только если нужен график динамики.
- Vitest + React Testing Library для UI/unit; Playwright для smoke/E2E. ESLint, Prettier.
- Дизайн: **светлая тема по умолчанию**, лаконичная enterprise-композиция, нейтральные поверхности, один спокойный акцентный цвет, без тяжёлых теней. Адаптация от 360px; приоритет desktop от 1280px.
- Frontend не имеет прямого доступа к PostgreSQL, EF Core, SMTP, приватным ключам и конфигурационным секретам.

Структура (расширение существующего solution):

```text
src/
  Identity.Domain/
  Identity.Application/
    Features/Admin/
      Auth/...
      Dashboard/GetDashboard/...
      Users/ListUsers/...
      Users/GetUserDetails/...
      Users/InviteUser/...
      Users/ResendInvitation/...
      Users/RequestPasswordReset/...
      Users/DisableUser/...
      Users/EnableUser/...
      Users/GetUserSessions/...
      Users/RevokeUserSession/...
      Users/RevokeAllUserSessions/...
      Audit/ListAuditEvents/...
  Identity.Infrastructure/
    Persistence/...
    Security/AdminSession/...
  Identity.Api/
    Controllers/Admin/
      AdminAuthController.cs
      AdminDashboardController.cs
      AdminUsersController.cs
      AdminAuditController.cs
  Identity.Admin.Web/
    src/
      app/                 routing, providers, theme
      api/                 httpClient, generated types
      features/auth/
      features/dashboard/
      features/users/
      features/audit/
      components/          common layout, loading, error, confirmation
      pages/               optional if needed
    public/
    index.html
    vite.config.ts
    package.json
    tsconfig.json
    Dockerfile
```

Не добавлять отдельный .NET Admin API: все административные endpoints размещаются в `Identity.Api` в `Controllers/Admin/`. Правила и обработчики — в `Application`, работа с EF/аутентификацией — в `Infrastructure`.

## 3. Административная авторизация

### 3.1. Системные административные разрешения

В Identity ввести собственное независимое от клиентских бизнес-приложений понятие **system administrators**:

- `IdentityAdministrator`: чтение и изменение пользователей, просмотр сессий и аудита.
- `IdentityReadOnly`: просмотр dashboard, пользователей, сессий и аудита, **без мутаций**.

Отдельная таблица `identity_administrators` (`user_id uuid PK/FK users`, `role smallint`, `granted_at timestamptz`, `granted_by uuid nullable`). Не добавлять эти роли в пользовательские access JWT по умолчанию. Эти роли не имеют отношения к ролям внешних приложений.

**Bootstrap:** создать локальную защищённую CLI-команду (например, отдельный `Identity.AdminCli` console project или deployment-only executable), которая назначает `IdentityAdministrator` уже зарегистрированному **Active + email-verified** пользователю по точному ID/email. Команда не доступна через публичный HTTP API, не содержит hardcoded секретов и записывает audit event. В prod явно запретить дефолтные логины/пароли. Отдельная команда для отзыва роли, но **запретить отзыв роли у последнего действующего администратора**.

### 3.2. Browser auth: отдельная admin cookie-сессия

Для UI использовать **same-origin HTTP-only session cookie**, а не хранение access/refresh JWT в `localStorage`/`sessionStorage`. Не нужно внедрять OAuth/OIDC в этот релиз.

- React и `/api/v1/admin` публиковать под **одним origin**, HTTPS обязательно в production.
- В ASP.NET Core использовать `CookieAuthentication` как транспорт защищённого auth ticket (не полноценный ASP.NET Core Identity). Cookie `HttpOnly`, `Secure`, `SameSite=Lax` или `Strict`, подходящий `Path`; никакие tokens/credentials не должны быть доступны JS.
- Дополнительно хранить серверную административную сессию `admin_sessions` (`id`, `user_id`, `created_at`, `last_seen_at`, `expires_at`, `revoked_at`) и ссылаться на неё по `sid` claim в защищённом cookie ticket. На каждом административном запросе (через validation event/authorization middleware) проверять user status, верификацию email, наличие admin role и отсутствие отзыва сессии; это гарантирует применение блокировок/снятия прав сразу. Хранить ASP.NET Core Data Protection key ring устойчиво вне процесса; при нескольких инстансах ключи общие.
- Время неактивности сессии по умолчанию 30 минут; абсолютная максимальная длительность 8 часов. Вход/выход и истечение сессии тестировать.
- Защита от CSRF обязательна на всех изменяющих admin запросах, **включая login/logout**. Реализовать ASP.NET Core antiforgery token endpoint, запрос из SPA передаёт токен в заголовке `X-CSRF-TOKEN`; сервер проверяет token. Login/refresh и прочие sensitive операции — rate limiting.
- Защита от brute force по IP и аккаунту, secure headers (`CSP`, `X-Content-Type-Options`, `Referrer-Policy`), ограничение CORS (для same-origin CORS не нужен), отсутствие секретов в логах.
- Пользователь без admin-role или со статусом не `Active` не получает admin cookie. Admin role нельзя назначить через self-register/self-update/API общего профиля.
- Для доступа из публичного интернета **до реализации MFA** размещать admin UI за VPN/доверенным сетевым контуром или эквивалентным механизмом. MFA для администраторов — обязательное требование перед свободной публичной публикацией, вне базового UI MVP.

### 3.3. Инварианты

- Запретить администратору блокировать или отзывать роль **последнего активного администратора** (атомарная проверка в транзакции).
- Блокировка учётной записи отзывает все серверные admin sessions и refresh sessions, прекращает будущие login/refresh. При проверке доступа к самому Identity API отключённые аккаунты также отклоняются.
- **Ограничение JWT:** автономно проверяемые клиентскими API короткоживущие JWT могут оставаться валидными до `exp` (15 минут в базовом ТЗ). Немедленная глобальная блокировка требует отдельной introspection/revocation-инфраструктуры и **не обещается** в этом дополнении.

## 4. Изменения доменной модели и БД

Изменить модели через миграции EF Core, не переписывая существующие таблицы и их данные.

1. Добавить `identity_administrators` и `admin_sessions` (см. выше), внешние ключи и индексы.
2. В `users` добавить `disabled_at timestamptz null`, `disabled_by uuid null`, `disable_reason varchar(500) null` (если уже есть эквиваленты — переиспользовать).
3. Для приглашений в `one_time_tokens.purpose` добавить `Invitation` (или отдельный subtype), TTL приглашения 72 часа по умолчанию, single use, hash-only в БД. Новые значения enum вводить с безопасной миграцией.
4. Добавить пользовательский статус `Invited` с отдельным значением, не переиспользовать `Disabled`/`Deactivated`. Статус `Invited` блокирует login до установки пароля.
5. Разрешить `users` существовать **без** `credentials` до принятия приглашения. `credentials.user_id` по-прежнему NOT NULL и FK; наличие `credentials` на пользователя необязательно. Проверять отсутствие credentials как invalid credentials, без 500.
6. Расширить `security_audit_events` полями `actor_user_id uuid nullable`, `target_user_id uuid nullable` (или эквивалентные безопасные metadata для фильтров с индексами). Регистрировать `admin_login`, `admin_invited_user`, `admin_resent_invitation`, `admin_requested_password_reset`, `admin_disabled_user`, `admin_enabled_user`, `admin_revoked_session`.
7. Поток приглашения: создаётся пользователь `Invited` без пароля + токен приглашения + зашифрованное outbox-сообщение **в одной транзакции**; получатель переходит по ссылке на публичную страницу `/account/activate`, устанавливает пароль, подтверждает владение почтой через токен и становится `Active`; повторный/просроченный token отклоняется.
8. Не помещать reset/invitation токены в plaintext лог, БД/outbox или URL query. Предпочтительный URL: `/account/activate#token=...`; фронтенд читает fragment, удаляет его из history до отправки POST JSON на API. Заголовок `Referrer-Policy: no-referrer`.

## 5. HTTP API, Controllers и контракты

Все маршруты ниже имеют prefix `/api/v1/admin`, возврат DTO без секретов, application/json. `[ApiController]`, attribute routing, CQRS dispatchers, unified ProblemDetails.

### 5.1. Authentication

| Method | Endpoint | Доступ | Эффект |
|---|---|---|---|
| GET | `/auth/csrf` | Anonymous | Выдать CSRF token для SPA + anti-forgery cookie |
| POST | `/auth/login` | Anonymous + CSRF | Email/password -> server-side admin session + HttpOnly cookie |
| GET | `/auth/me` | Admin/ReadOnly | `{id,email,displayName,role}` |
| POST | `/auth/logout` | Admin/ReadOnly + CSRF | Отозвать сессию, удалить cookie |

### 5.2. Dashboard and read queries

| Method | Endpoint | Доступ | Эффект |
|---|---|---|---|
| GET | `/dashboard` | Admin/ReadOnly | Общая статистика + ряд регистраций за 30 дней |
| GET | `/users` | Admin/ReadOnly | Страница пользователей с фильтрами |
| GET | `/users/{userId:guid}` | Admin/ReadOnly | Детали пользователя |
| GET | `/users/{userId:guid}/sessions` | Admin/ReadOnly | Активные/недавние сессии пользователя |
| GET | `/audit-events` | Admin/ReadOnly | Лента аудита с фильтрами и пагинацией |

Query `/users`: `page` (1-based), `pageSize` (default 25, max 100), `search` (max 200), `status`, `emailVerified`, `sortBy` (`createdAt|email|status`), `sortDirection` (`asc|desc`). Для sort — исключительно whitelisted поля (никакого построения произвольных SQL identifiers). Формат:

```json
{
  "items": [{"id":"00000000-0000-0000-0000-000000000001","email":"user@example.com","displayName":"Example User","status":"Active","emailVerified":true,"createdAt":"2026-01-01T00:00:00Z"}],
  "page": 1,
  "pageSize": 25,
  "total": 1
}
```

Dashboard DTO: `{totalUsers,activeUsers,pendingVerificationUsers,invitedUsers,disabledUsers,deactivatedUsers,activeSessions,registrationTrend:[{date,count}]}`. `totalUsers` — все состояния, остальные user counts — взаимоисключающие. `activeSessions` — невозванные и ещё не истёкшие сессии на момент подсчёта.

### 5.3. Admin commands

| Method | Endpoint | Доступ | Эффект |
|---|---|---|---|
| POST | `/users/invitations` | IdentityAdministrator | Пригласить пользователя, `202 Accepted` |
| POST | `/users/{userId:guid}/invitations/resend` | IdentityAdministrator | Новый приглашение-token, `202` |
| POST | `/users/{userId:guid}/password-reset` | IdentityAdministrator | Создать reset token + email, `202` |
| POST | `/users/{userId:guid}/disable` | IdentityAdministrator | `{reason:string}`, `204` |
| POST | `/users/{userId:guid}/enable` | IdentityAdministrator | Снять ручную блокировку, `204` |
| DELETE | `/users/{userId:guid}/sessions/{sessionId:guid}` | IdentityAdministrator | Отозвать сессию, `204` |
| POST | `/users/{userId:guid}/sessions/revoke-all` | IdentityAdministrator | Отозвать все сессии пользователя, `204` |

Приглашение пользователя, который уже существует, возвращает `409 Conflict`, если политика не предполагает повторного использования существующей учётной записи. Допускается только явный resend для `Invited`. Отправка письма через existing outbox; успех HTTP не означает гарантированную SMTP-доставку.

`disable` и `enable` — Domain transitions с идемпотентностью/конфликтами, а не прямое редактирование `status` в контроллере. Разблокировка возвращает предыдущее допустимое состояние либо явно `Active` только для ранее верифицированной учётной записи; не активировать неподтверждённый email. Деактивированные аккаунты не реанимировать автоматически.

Публичные (не admin) endpoints для acceptance of invitation и установки начального пароля добавить под `/api/v1/auth/invitations/accept`; страницы `/account/activate` и `/account/reset-password` в том же SPA доступны без административных прав, не раскрывают административные данные.

## 6. Web UI и UX

Основные маршруты:

```text
/admin/login                  admin login
/admin                        dashboard
/admin/users                  searchable/paginated users table
/admin/users/:id              user details + sessions + security activity
/admin/audit                  security audit log
/account/activate             public acceptance of admin invitation
/account/reset-password       public password reset flow
```

### Layout

- Responsive fixed/sidebar layout (collapsed at narrower widths), topbar с идентификатором администратора и logout.
- `Identity Console` branding; светлая тема. Ant Design ConfigProvider tokens в одной теме, использовать системные шрифты. Не копировать стили из чужих проектов.
- В каждой таблице: серверные filters/sort/page, debounced search 300–500 мс, возможность сбросить фильтры, loading, error, empty state.
- `Users` table: Email, Name, Status Tag, Email verified, Created, Actions; действия через overflow menu. Действия недоступны ReadOnly и недопустимы для текущего состояния пользователя.
- `User details`: readonly свойства, status, email verified, sessions table, недавние audit events и отдельные подтверждения для опасных действий.
- `Invite user`: modal с email, display name (optional), пояснение про приглашение; подтверждение перед отправкой.
- `Disable`: обязательное поле reason, подтверждение, запрос только после подтверждения; сообщения об успехе/ошибке с учётом фактического ответа backend.
- `Password reset`: подтверждение отправки ссылки, **не** показывать пароль, reset token или hash.
- Всегда показывать UTC-даты в локальной таймзоне клиента с явным tooltip/format; передавать ISO UTC.
- Никаких моковых данных в production. Допустимы только test fixtures в тестах или в явно локальном dev режиме.

### Client integration

- React Query keys на основе параметров поиска/фильтров, инвалидировать dashboard/users/user details после успешных мутаций.
- `fetch` с `credentials: 'same-origin'`; CSRF token запрашивается/передаётся централизованно для всех unsafe методов.
- 401: переход на admin login и очистка query cache; 403: отдельный forbidden screen, не бесконечный login loop; 409: понятное business conflict сообщение.
- Включить ErrorBoundary и стандартные состояния ожидания/нет данных/ошибки.
- Не сохранять пароли, JWT, reset tokens, приглашения в localStorage, sessionStorage и analytics/events.

## 7. Deployment

- Собрать Vite статические файлы в multi-stage Dockerfile `Identity.Admin.Web` и отдавать через Nginx либо безопасную публикацию статики.
- **Один публичный origin** (например, `https://identity.example.com`): `/admin/*`, `/account/*` -> SPA; `/api/*` -> `Identity.Api`; `/health/*` по необходимости.
- Nginx fallback to `index.html` только для SPA-маршрутов, **никогда** для `/api/*` (не подменять 404 API HTML-страницей).
- `Cache-Control: immutable` только для hashed assets, entry `index.html` — no-cache. HTTPS/HSTS в prod, Content-Security-Policy, Referrer-Policy, no secrets in frontend bundle.
- Compose обновить сервисом `identity-admin-web`; тестовая dev инфраструктура работает с существующим PostgreSQL и Mailpit.
- Не открывать прямой административный API в обход VPN/сетевого контура, пока нет MFA; те же ограничения для admin SPA и всех admin endpoints.
- Инструкция bootstrap первого админа, ротации admin session keys/Data Protection, резервного копирования, блокировки admin account, аварийного восстановления.

## 8. План Codex: этапы 8–12 (после этапов 0–7 исходного ТЗ)

### Этап 8. Admin domain & secure access

- Domain + migrations `identity_administrators`, `admin_sessions`, `Invited`, invitation token purpose, disable metadata, audit actor/target.
- Application abstractions/use cases для админ-логина/выхода; Cookie Auth, CSRF, session validation, Authorization policies `IdentityAdmin`, `IdentityAdminRead`.
- CLI bootstrap/revoke admin; последний админ защищён от удаления/блокировки.
- Tests: нет админ-прав => 403, disabled admin => отказ, cookie secure attributes, CSRF required, cookie revocation, concurrent revoke of last admin.
- **Acceptance:** админ после CLI grant входит через secure cookie и получает `/api/v1/admin/auth/me`; посторонний не получает доступ, cookie-session отзывается.

### Этап 9. Read-only Admin API

- `GetDashboardQuery`, `ListUsersQuery`, `GetUserDetailsQuery`, `ListAuditEventsQuery`, `GetUserSessionsQuery`, handlers + controllers.
- Server-side pagination, whitelist sorts, filters, DTO-only projections, индексы/предсказуемые SQL-запросы.
- Integration/API tests на число строк, edge-case empty DB, page boundaries, timezone, read-only permission.
- **Acceptance:** OpenAPI содержит admin read API; статистика и список корректны на seeded test dataset, performance пригодна без N+1.

### Этап 10. Administrative write commands

- `InviteUserCommand`, `ResendInvitationCommand`, `RequestAdminPasswordResetCommand`, `DisableUserCommand`, `EnableUserCommand`, `RevokeUserSessionCommand`, `RevokeAllUserSessionsCommand`.
- Invitation acceptance, primary password setup, outbox delivery, audit actor/target, status invariants.
- Integrations: disable/revoke/block existing refresh + admin sessions, public access checks, JWT expiry limitation documented.
- Tests: invite duplicate/race, token expiry/reuse, absence of credentials, blocked user no login, last-admin protection, transactional outbox, no secrets in audit.
- **Acceptance:** CRUD-like operations work end-to-end with Mailpit; API never returns passwords or one-time tokens to admin.

### Этап 11. React Admin Web

- React + TS + Vite + Ant Design 6 + TanStack Query frontend, routing, light theme.
- Login, dashboard, users table, user details, invitation/disable/reset modals, sessions, audit, public invitation acceptance/reset pages.
- Generated API types, centralized cookie/CSRF fetch, protected UI routes, role-aware action affordances.
- Unit/component tests + Playwright happy path and failure scenarios.
- **Acceptance:** UI build/lint/tests pass; no token persistence in browser storage; SPA reflects backend state after actions.

### Этап 12. Production hardening & release

- E2E API+frontend+PostgreSQL+Mailpit; restricted admin deployment; CSP/HTTPS/cookie settings, reverse proxy config, antiforgery and negative security tests.
- Docker Compose, CI build/lint/tests, OpenAPI, deployment guide, admin bootstrap manual, runbook, release notes.
- **Acceptance:** new environment deploys reproducibly; no default admin password; security tests pass; protected actions return correct 401/403/CSRF errors; all secrets external to repository.

## 9. Правила выполнения каждого этапа

1. В начале этапа прочитать основной Spec + это дополнение + `AGENTS.md` + текущий git status + `IMPLEMENTATION_STATUS.md` при наличии.
2. До реализации описать кратко предполагаемые изменения; выполнить **только один** этап за запуск.
3. Не удалять и не ломать текущие user login/refresh/registration/endpoints.
4. После работы выполнить доступные build, unit, integration и frontend lint/test команды, сообщить фактически выполненные проверки.
5. Обновить `IMPLEMENTATION_STATUS.md`: изменения, выполненные тесты, архитектурные решения, остаточные проблемы, следующий этап.
6. Обязательно подготовить резюме файлов/миграций/API/конфигов. Следующий этап только после отдельного запроса.

## 10. Минимальный критерий готовности всей админки

- Administrator входит и видит реальные значения dashboard.
- ReadOnly читает, но ни одна изменяющая операция ему недоступна на backend.
- Администратор может отыскать пользователя, пригласить нового по email, инициировать reset через email, блокировать/разблокировать, отзывать сессии.
- Приглашённый задаёт пароль через одноразовую безопасную ссылку; никакой plaintext пароль администратору не известен.
- Заблокированный пользователь не может войти/обновить сессию; действующий admin-cookie немедленно отклоняется; автономные внешние JWT остаются потенциально валидными только до `exp`.
- Журнал аудита содержит actor/target/action/timestamp, но не содержит паролей и токенов.
- CI собирает backend и frontend, тесты проходят, Compose запускает работающую систему, задокументирован безопасный bootstrap админа.
