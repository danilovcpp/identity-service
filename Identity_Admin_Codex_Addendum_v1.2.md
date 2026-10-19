# Identity Admin Console — дополнение к Identity_Codex_Spec.md

**Версия:** v1.2 (планируемая; детализация фронтенд-этапа 11)  
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

## 8. План Codex: этапы 8–12, этап 11 разделён на 14 подэтапов

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

### Этапы 11.1–11.14. Identity Admin Web — последовательная реализация frontend

**Порядок:** запускать каждый подпункт 11.x **как отдельное задание Codex**, завершать отчётом и останавливаться. Для выполнения 11.x нужны завершённые backend-этапы 8–10 и доступный OpenAPI контракт; если backend не готов, фиксировать блокер, а не подменять отсутствующие API заглушками. Следующий этап только по команде человека. Этап 12 выполняется только после приёмки 11.14.

**Общие правила для всех подэтапов:**

- Продукт называется `Identity`, UI называется `Identity Console`. Никаких зависимостей от внешней предметной области.
- React 19, TypeScript strict, Vite, Ant Design 6, TanStack Query, React Router, Fetch, Vitest/RTL; Playwright — для пользовательских сценариев. Не добавлять второй UI kit или React Admin.
- Компоненты UI не должны делать прямой `fetch`: запросы оформлять в `src/api`, интеграцию — в `src/features/<feature>/api` и Query hooks. Данные frontend — только из backend. Mock Service Worker/fixtures допустимы лишь в тестах, никогда как production fallback.
- Использовать same-origin `/api/v1/...` через reverse proxy; в dev Vite proxy `/api` к локальному `Identity.Api`, по возможности HTTPS. Cookies передавать с `credentials: 'same-origin'`, CSRF на unsafe admin-методах; никаких JWT/refresh/one-time tokens в browser storage. Не отключать серверную проверку CSRF, даже временно.
- Чтение прав в UI только для доступности действий. Решение об авторизации всегда принимает backend. `IdentityReadOnly` видит только разрешённые действия чтения.
- Каждая страница поддерживает loading / empty / error / success, понятные `401` / `403` / `409`, адаптивность и клавиатуру. После успешных команд инвалидация конкретных TanStack Query keys, а не перезагрузка всего SPA.
- Не расширять API, миграции, CQRS backend и security policy в рамках frontend-подэтапа. При несовместимости зафиксировать точную проблему, требуемый контракт и остановить данный подэтап.
- В конце каждого подэтапа: проверить `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build` (когда скрипты существуют), релевантные Playwright тесты; обновить `IMPLEMENTATION_STATUS.md` с номером этапа, файлами, проверками, известными ограничениями. Не утверждать, что тесты прошли, если не запускались.

#### 11.1. Bootstrap проекта и инструменты разработки

**Задача:** создать работающую основу фронтенда, не реализуя доменные экраны.

**Сделать:**

1. Создать `src/Identity.Admin.Web` на Vite + React + TS (`strict: true`, `noUncheckedIndexedAccess` по возможности). Добавить зависимости из раздела 2, lockfile и скрипты `dev`, `build`, `typecheck`, `lint`, `format:check`, `test`, `test:run`.
2. Установить ESLint, Prettier, Vitest, React Testing Library, jsdom. Настроить path alias `@/` и единый импортный стиль.
3. Вынести минимальную структуру: `src/app`, `src/api`, `src/components`, `src/features`, `src/pages`, `src/test`.
4. Настроить Vite dev proxy `/api` на локальный backend без секретов в браузерном bundle; `.env.example`, `README.md` с командами запуска, `gitignore`.
5. Создать точку входа `main.tsx`, подключить React и базовую страницу с названием `Identity Console` (без фальшивых метрик и логина-заглушки). Добавить один реальный smoke unit test на рендер приложения.

**Результат:** SPA запускается, страница отображается, `vite build` формирует `dist`, базовый тест выполняется.

**Критерии приёмки:** `npm ci`, typecheck/lint/unit/build зелёные; в bundle нет API-секретов; нет запросов к БД и моковых бизнес-данных.

**Не делать:** API-клиент, routing с закрытыми разделами, авторизацию, Dashboard.

#### 11.2. Типизированный HTTP-клиент, OpenAPI, CSRF

**Задача:** сделать единственный безопасный слой обмена с Identity API.

**Сделать:**

1. Подключить генерацию TypeScript types по OpenAPI backend (`openapi-typescript` или эквивалент без runtime SDK); добавить явную команду `api:generate`. Фиксировать несовпадения схем, не выдумывать response fields.
2. Реализовать `apiClient` с `credentials: 'same-origin'`, `AbortSignal`, JSON, а также пустыми ответами `204` и `202` (если сервер не возвращает тело), таймаутом/отменой на вызывающей стороне, нормализацией `ProblemDetails` и статусов `400/401/403/404/409/429/5xx`.
3. Реализовать получение CSRF через `GET /api/v1/admin/auth/csrf`, хранение anti-forgery request token **только в памяти** и автоматическое добавление заголовка `X-CSRF-TOKEN` ко всем unsafe admin-запросам, включая login/logout; соблюдать реальную форму ответа endpoint.
4. Не повторять автоматически POST/DELETE после сетевого сбоя, 401 или CSRF-ошибки; не выполнять бесконечную перерегистрацию CSRF. Выделить различие между API-ошибкой и отсутствием сети.
5. Добавить TanStack Query `QueryClient`, единые query-key factories; global QueryClient не должен протекать между unit tests.

**Результат:** `src/api` с типизированным клиентом, генерацией контрактов и CSRF-механизмом, пока без экранов пользователей.

**Критерии приёмки:** тесты fake-fetch/MSW подтверждают `GET`, `POST` с заголовком CSRF, `204` без JSON, `401`/`403`/`409`, ошибку сети, отсутствие автоматического повтора небезопасной команды. Токены и cookie не логируются.

**Не делать:** login screen и features; подмена реального backend фейковыми production ответами.

#### 11.3. UI-тема, навигация и адаптивный App Shell

**Задача:** сформировать переиспользуемый внешний вид админки.

**Сделать:**

1. Добавить `ConfigProvider` Ant Design, централизованные theme tokens (светлая тема, одна accent color, шрифты, отступы, радиусы, neutral surfaces). Стили не разбрасывать по страницам.
2. Реализовать `AdminLayout`: sidebar, topbar, зона контента, компактный mobile drawer, breadcrumb/title слот, контейнеры стандартных отступов.
3. Вынести общие `PageHeader`, `LoadingState`, `EmptyState`, `ErrorState`, `ConfirmAction`, `StatusTag` (последний только на основании backend status, без выдуманных статусов).
4. Настроить React Router: общий `NotFound`, базовые публичные и admin layout branches. Навигацию к ещё не реализованным страницам **не активировать**.
5. Сделать responsive поведение на 360, 768 и 1280+ px, корректную видимость focus, tab-navigation, aria-label у icon-only элементов.

**Результат:** единая оболочка `Identity Console` с работающим меню/мобильной навигацией и компонентами состояний, без фиктивного Dashboard.

**Критерии приёмки:** визуальная проверка трех размеров, keyboard smoke, unit tests для shell/empty/error/navigation, все проверки сборки проходят.

**Не делать:** аутентификацию, backend данные или формы действий.

#### 11.4. Административные Login / Logout / Route Guards

**Задача:** подключить реальную защищённую административную сессию.

**API:** `GET /api/v1/admin/auth/csrf`, `POST /api/v1/admin/auth/login`, `GET /api/v1/admin/auth/me`, `POST /api/v1/admin/auth/logout`.

**Сделать:**

1. `/admin/login`: поля email/password, валидация ввода, submitting/error states, защита от повторной отправки, только backend authentication (без hardcoded accounts).
2. На старте приложения проверять `/auth/me`, корректно различать loading, authenticated, unauthenticated, forbidden. Redirect с защищённых страниц на login, сохранение безопасного внутреннего return path (без open redirect).
3. По успешному login обновлять профиль в Query cache и переходить в `/admin`. По logout отправлять CSRF-protected запрос, завершать сессию и очищать sensitive query cache.
4. ReadOnly и Administrator хранить в auth context из `/auth/me` как UI capability; закрыть все admin routes для отсутствия session. При `401` корректно завершать UI-сессию, при `403` показывать Forbidden и избегать redirect loop.
5. Не сохранять пароль, токены или auth ticket в localStorage/sessionStorage. JS не должен читать HttpOnly cookie.

**Результат:** администратор входит, видит защищённую оболочку и выходит; unauthenticated пользователь не видит закрытые разделы.

**Критерии приёмки:** component tests success/error/401/403/readOnly/logout, Playwright smoke с реальным admin test account (если тестовая среда доступна), refresh страницы сохраняет авторизацию только через серверную cookie.

**Не делать:** dashboard и пользовательские CRUD-экраны.

#### 11.5. Dashboard со статистикой

**Задача:** первая полноценная read-only бизнес-страница UI.

**API:** `GET /api/v1/admin/dashboard`.

**Сделать:**

1. На `/admin` показать общее количество, Active, PendingVerification, Invited, Disabled, Deactivated, активные сессии — только поля фактического DTO.
2. Добавить график регистраций за последние 30 дней на Recharts; подписи дат, tooltip, понятную подпись периода и единицы измерения.
3. Показать понятные loading/empty/error states; retry кнопка только для GET, никаких фиктивных fallback чисел.
4. Учитывать доступ `IdentityReadOnly` и `IdentityAdministrator` одинаково для чтения; форматировать даты и числа в выбранной локали.

**Результат:** `/admin` отображает живые показатели из Identity, обновляемые при refetch.

**Критерии приёмки:** тесты на 0 пользователей, заполненный DTO, ошибку API; E2E подтверждает, что реальные данные появились после server-side изменений.

**Не делать:** действия над аккаунтами.

#### 11.6. Пользователи: таблица и поиск

**Задача:** рабочая страница каталога пользователей.

**API:** `GET /api/v1/admin/users`.

**Сделать:**

1. `/admin/users`: Ant Design Table c Email, DisplayName, Status, EmailVerified, CreatedAt; индикатор общего количества и ссылка на details.
2. Серверные `page` (с 1), `pageSize` (25 по умолчанию, не больше 100), `search` с debounce 300–500 мс, filters status/verified, whitelist sort (`createdAt|email|status` + direction).
3. Синхронизировать page/search/filters/sort с URL query params; назад/вперёд браузера возвращает то же состояние. При изменении поиска/фильтра переходить на страницу 1.
4. Query key содержит все server-side параметры; не сортировать вручную только текущую страницу, не делать клиентскую пагинацию по одному response.
5. Адаптировать таблицу под узкий экран, обеспечить loading/empty/error/retry/reset filters. Никаких массовых действий.

**Результат:** можно найти пользователя по email/name, отфильтровать и открыть карточку без перезагрузки SPA.

**Критерии приёмки:** тесты debounce, page boundaries, URL roundtrip, пустой ответ, failure; серверный API вызывается с корректными query params; проверка мобильного layout.

**Не делать:** приглашения, block/reset, редактирование данных.

#### 11.7. Карточка пользователя: только чтение

**Задача:** подробный экран пользователя без административных мутаций.

**API:** `GET /api/v1/admin/users/{id}`, чтение связанных audit events — только если поддерживает существующий контракт `GET /api/v1/admin/audit-events`.

**Сделать:**

1. `/admin/users/:id`: email, display name, status, email verification, даты создания и изменения (если входят в DTO), ID с copy action.
2. Отображать незаполненные поля корректно; дату в локальном времени с UTC tooltip. Не показывать никакие password hashes, raw tokens, секреты, лишние audit metadata.
3. Подключить read-only список последних событий пользователя, если фильтрация доступна в API; если контракт не поддерживает, не делать client-side загрузку полного аудита «для поиска».
4. Добавить возврат в таблицу с сохранением фильтров (из URL); not-found, invalid UUID, loading/error. Состояния/тексты действий пока не активировать.

**Результат:** администратор может открыть карточку пользователя и понять состояние учётной записи.

**Критерии приёмки:** unit/component tests null fields, status variations, 404, navigation back; тест безопасности на отсутствие sensitive fields в DOM.

**Не делать:** редактирование, sessions revoke, reset или disable.

#### 11.8. Приглашение и повторная отправка приглашения

**Задача:** первый законченный write сценарий через UI.

**API:** `POST /api/v1/admin/users/invitations`, `POST /api/v1/admin/users/{id}/invitations/resend`.

**Сделать:**

1. На `/admin/users` кнопку Invite user только для `IdentityAdministrator`; Ant Design modal: email (required), displayName (optional), пояснение, что пароль устанавливает получатель.
2. Создать mutation с CSRF, disable submit пока запрос выполняется, обработать ошибки валидации и `409 Conflict`, не допустить двойной submit.
3. После `202` сообщать «Приглашение принято в обработку», не обещать SMTP-доставку; закрывать форму/инвалидировать users/dashboard при успехе.
4. Resend invitation показывать только для пользователя в состоянии `Invited` с ролью Administrator; подтверждение, `202` feedback, инвалидировать detail/audit. Backend может отклонить действие — обработать.

**Результат:** администратор приглашает нового пользователя, он появляется в таблице со статусом `Invited`; повторная отправка доступна в карточке.

**Критерии приёмки:** тесты validation, 202, 409, readonly (кнопки отсутствуют), двойной submit; интеграционный smoke с Mailpit по возможности.

**Не делать:** публичную страницу принятия приглашения (это 11.13), автоматическое назначение пароля.

#### 11.9. Блокировка и разблокировка пользователя

**Задача:** безопасное управление статусом.

**API:** `POST /api/v1/admin/users/{id}/disable`, `POST /api/v1/admin/users/{id}/enable`.

**Сделать:**

1. В карточке и/или actions меню списка добавить Disable/Enable, показывать только допустимое действие в зависимости от роли и текущего статуса.
2. Disable: подтверждающее модальное окно и обязательная причина длиной до 500 символов; явно пояснить последствия для сессий.
3. Enable: отдельное подтверждение; frontend не должен сам выбирать новый статус — результат определяется сервером.
4. Обработать `204`, `403`, `409`, включая защиту последнего администратора и конфликт изменившегося статуса. Не считать отключение успешным до HTTP ответа.
5. Инвалидировать user detail, users page, dashboard, sessions, audit. В ReadOnly все write controls отсутствуют.

**Результат:** пользователь блокируется/разблокируется через UI, актуальный статус обновляется после ответа сервера.

**Критерии приёмки:** tests forbidden state/no permission/required reason/409/204; E2E пользователь больше не может выполнить новый вход после block.

**Не делать:** изменение ролей администраторов и действия над последним admin вне серверных гарантий.

#### 11.10. Запрос сброса пароля администратором

**Задача:** контролируемый инициированный администратором password reset.

**API:** `POST /api/v1/admin/users/{id}/password-reset`.

**Сделать:**

1. Добавить действие Send password reset link в User Details (только Administrator) с предупреждением: письмо отправляется пользователю; администратор не знает пароль.
2. Подтверждать действие, не выполнять запрос при простом открытии модального окна. Поддержать pending, `202`, `403`, `409`, network error.
3. При `202` показывать факт постановки запроса в обработку, а не обещать доставку; инвалидировать audit.
4. Не включать пароль или one-time token в DTO, UI, console, request logs и analytics; никаких полей для ввода нового пароля администратором.

**Результат:** администратор запрашивает письмо со ссылкой на reset, UI показывает корректное состояние операции.

**Критерии приёмки:** component tests confirmation/no direct reset/readOnly/202/errors; audit event появляется после операции.

**Не делать:** пользовательскую reset-страницу (11.13).

#### 11.11. Управление пользовательскими сессиями

**Задача:** чтение и отзыв сессий конкретного пользователя.

**API:** `GET /api/v1/admin/users/{id}/sessions`, `DELETE /api/v1/admin/users/{id}/sessions/{sessionId}`, `POST /api/v1/admin/users/{id}/sessions/revoke-all`.

**Сделать:**

1. На User Details отдельная секция Sessions: created/lastUsed/expiry/revoked/current-state по фактически доступным полям DTO; не изобретать поля устройства/IP, если их нет.
2. Операция Revoke session в строке и Revoke all sessions в заголовке; разные подтверждения, включая число затрагиваемых сессий лишь когда оно точно известно.
3. Мутации разрешены только Administrator; `204` → refresh sessions, user detail, dashboard, audit; `403/404/409` → понятное сообщение.
4. Поддержать пустые/просроченные/отозванные сессии, корректное локальное форматирование дат, scrolling/compact display для мобильных размеров.

**Результат:** можно увидеть действующие сессии пользователя и отозвать одну или все.

**Критерии приёмки:** tests correct sessionId in route, revoke-one vs revoke-all, readonly, empty, errors; E2E после отзыва refresh пользовательской сессии отклоняется backend.

**Не делать:** отдельную глобальную страницу сессий без API-контракта.

#### 11.12. Журнал аудита

**Задача:** полноценный read-only раздел security audit.

**API:** `GET /api/v1/admin/audit-events`.

**Сделать:**

1. `/admin/audit`: таблица OccurredAt (локальное время + UTC tooltip), EventType, Actor, Target, CorrelationId/metadata — только безопасные разрешённые поля.
2. Server-side pagination и фильтры по actor, target, event type, периоду — согласно фактическому API; хранить фильтры в URL search params.
3. Ссылка на User Details по actor/target при наличии корректного ID; пустые значения показывать явно, не как `undefined`.
4. Настроить loading/empty/error/retry; нормализовать известные event type labels, безопасные metadata рендерить текстом, не HTML.

**Результат:** администратор/ReadOnly видит реальные события безопасности и находит действия по пользователю и дате.

**Критерии приёмки:** тесты фильтрации, URL-параметров, read-only, HTML escaping, 0 events и ошибки; время и порядок совпадают с API.

**Не делать:** backend экспорт логов, произвольный JSON редактор, массовые удаления.

#### 11.13. Публичные страницы активации и смены пароля

**Задача:** закончить email-driven flows для конечного пользователя без admin-доступа.

**API:** `POST /api/v1/auth/invitations/accept` (дополнение), `POST /api/v1/auth/password/reset` (базовая спецификация).

**Сделать:**

1. `/account/activate`: считать одноразовый invitation token из `location.hash`, **немедленно удалить fragment из адреса через `history.replaceState`** до запуска аналитики или других побочных эффектов; держать его только в памяти компонента до submit.
2. Форма установки первоначального пароля и подтверждения; policy hint соответствует backend. После успеха показать завершение и ссылку на корректный вход, не выполнять скрытый auto-login.
3. `/account/reset-password`: аналогичная безопасная обработка токена из `#token=...`, поля password/confirmation, отправка токена только как JSON body на documented API.
4. Прямой переход без token, expired/consumed/invalid token, слабый пароль, несовпадение подтверждения, ошибки сети — отдельные понятные states без раскрытия секрета.
5. Формы доступны анонимно и **не требуют** admin cookie; пароль/token нельзя писать в Query cache, session/localStorage, console или tracking, а ссылка не должна попасть в логи.

**Результат:** приглашённый пользователь устанавливает пароль по email, а пользователь со ссылкой восстановления меняет пароль; закрытый кабинет администратора не требуется.

**Критерии приёмки:** E2E с Mailpit для invite accept и password reset при наличии инфраструктуры; tests hash cleanup, invalid token, replay/error, submission, отсутствие токенов в browser storage.

**Не делать:** регистрацию публичного пользователя через admin UI, новый backend authentication flow.

#### 11.14. Сквозная доработка frontend и приёмка

**Задача:** довести готовые страницы до согласованного production-oriented UX, не выполняя ещё инфраструктурный этап 12.

**Сделать:**

1. UX ревизия всех страниц: адаптивность 360/768/1280+ px, keyboard only, focus management после modal, aria label, contrast, понятные placeholders, i18n-ready подход без обязательного многоязычия.
2. Исключить дублирующиеся компоненты и случайные локальные `fetch`, привести общий API error presentation и toast notifications к одной политике; устранить query-cache stale states.
3. В Playwright покрыть: admin login/logout; readOnly не видит действий; dashboard; pagination/search; invite/resend; disable/enable; reset-request; revoke sessions; audit; public invite-activation/password-reset; 401/403/409/CSRF ошибки. Использовать тестовый backend и данные, очищаемые между прогонами.
4. Проверить отсутствие credentials/token в localStorage/sessionStorage, frontend bundle, browser console, URL query, скриншотах/логах тестов; cookie HttpOnly проверяется server/browser, а не JS.
5. Обновить `README` frontend с env, dev proxy, командами typecheck/lint/unit/e2e/build, скриншотами/примерами навигации; итоговый отчёт со списком сценариев.

**Результат:** функционально законченная Identity Console; frontend готов для отдельного этапа 12 (Docker/proxy/CI/prod hardening).

**Критерии приёмки:** typecheck/lint/unit/build зелёные, Playwright smoke + негативные сценарии проходят в доступной инфраструктуре; ни один protected admin route не доступен анонимно; нет фиктивных production данных; Lighthouse/axe warnings при наличии средств перечислены и критичные исправлены.

**Не делать:** изменять исходное API, устанавливать MFA, проводить внешнее развертывание, переопределять reverse proxy security; это задача отдельной приёмки и этапа 12.

**Правило передачи между этапами:** в `IMPLEMENTATION_STATUS.md` после каждого 11.x фиксировать `Completed: 11.x`, `Commands run`, `Observed results`, `Known issues`, `Next: 11.y`. Следующий чат Codex должен читать этот статус и спецификации и реализовывать **только указанный подэтап**, даже если видит оставшиеся функции продукта.

### Этап 12. Production hardening & release

- E2E API+frontend+PostgreSQL+Mailpit; restricted admin deployment; CSP/HTTPS/cookie settings, reverse proxy config, antiforgery and negative security tests.
- Docker Compose, CI build/lint/tests, OpenAPI, deployment guide, admin bootstrap manual, runbook, release notes.
- **Acceptance:** new environment deploys reproducibly; no default admin password; security tests pass; protected actions return correct 401/403/CSRF errors; all secrets external to repository.

## 9. Правила выполнения каждого этапа

1. В начале этапа прочитать основной Spec + это дополнение + `AGENTS.md` + текущий git status + `IMPLEMENTATION_STATUS.md` при наличии.
2. До реализации кратко описать предполагаемые изменения; выполнить **только один** этап за запуск. Для frontend это означает один конкретный подпункт `11.x`, а не весь этап 11.
3. Не удалять и не ломать текущие user login/refresh/registration/endpoints.
4. После работы выполнить доступные build, unit, integration и frontend lint/test команды, сообщить фактически выполненные проверки.
5. Обновить `IMPLEMENTATION_STATUS.md`: изменения, выполненные тесты, архитектурные решения, остаточные проблемы, следующий этап.
6. Обязательно подготовить резюме файлов/миграций/API/конфигов. Следующий этап/подэтап `11.x` только после отдельного запроса.

## 10. Минимальный критерий готовности всей админки

- Administrator входит и видит реальные значения dashboard.
- ReadOnly читает, но ни одна изменяющая операция ему недоступна на backend.
- Администратор может отыскать пользователя, пригласить нового по email, инициировать reset через email, блокировать/разблокировать, отзывать сессии.
- Приглашённый задаёт пароль через одноразовую безопасную ссылку; никакой plaintext пароль администратору не известен.
- Заблокированный пользователь не может войти/обновить сессию; действующий admin-cookie немедленно отклоняется; автономные внешние JWT остаются потенциально валидными только до `exp`.
- Журнал аудита содержит actor/target/action/timestamp, но не содержит паролей и токенов.
- CI собирает backend и frontend, тесты проходят, Compose запускает работающую систему, задокументирован безопасный bootstrap админа.
