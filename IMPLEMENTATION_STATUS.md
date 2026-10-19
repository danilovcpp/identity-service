# Отчёт по этапу 8

## 1. Этап

**8 — Admin domain & secure access**, завершён 2026-10-19.
Прочитаны AGENTS.md, Identity_Codex_Spec.md, Identity_Admin_Codex_Addendum.md,
предыдущий IMPLEMENTATION_STATUS.md; изучены код и Git history. Этапы 0–7 подтверждены
commits d7c4f94, 8d901d7, 647ae4c, 4f64b57, 6e793da, 32fa225, 7e9fb07, c5f230d.
Изначально единственным untracked файлом был предоставленный пользователем addendum;
он не изменялся. Реализован только этап 8 дополнения, этапы 9–12 не начинались.

Дополнение явно расширяет прежний scope v1.0: собственные системные администраторы
Identity допустимы по §0/§3 addendum, роли внешних приложений по-прежнему запрещены.
AGENTS.md уточнён для этапов 8–12. Версия выпуска 1.0.0 не переобъявлена релизом v1.1:
новая функциональность описана в CHANGELOG как Unreleased.

## 2. Реализовано

### Domain и PostgreSQL

- IdentityAdministrator: user_id PK/FK, IdentityAdministrator/IdentityReadOnly, granted_at,
  granted_by. Grant только Active + email-verified. Роли не добавляются в пользовательские JWT.
- AdminSession: отдельные id/user_id/created_at/last_seen_at/expires_at/revoked_at,
  доменные проверки сроков, xmin, индексы user/revocation и expiry.
- Invited=4 и Invitation=2 без перенумерации прежних значений; Invitation maximum TTL 72 часа,
  hash-only/single-use через существующий OneTimeToken. Domain factory для Invited.
  Схема допускает отсутствие credentials; оба login безопасно отклоняют такой аккаунт.
- users: nullable disabled_at/disabled_by/disable_reason(500); доменное заполнение metadata,
  FK/check constraints. Legacy metadata не выдумывается и остаётся null.
- security_audit_events: nullable actor_user_id/target_user_id, FK и индексы с occurred_at;
  расширена фиксированная таксономия admin events. Реально создаются admin_login,
  admin_login_failed, admin_logout, admin_role_granted, admin_role_revoked. Имена событий
  приглашений/блокировок подготовлены схемой, их use cases/HTTP endpoints пока отсутствуют.
- Новая additive migration: **20261019020843_AdminSecureAccess** + Designer/snapshot.
  Существующие таблицы/строки не заменяются; миграции не запускаются автоматически API.

### Application / Infrastructure / API

- Вертикальные срезы AdminLogin, AdminLogout, ValidateAdminSession, GetAdminMe,
  CLI-only GrantAdministrator/RevokeAdministrator. Typed handlers/validators,
  явная DI-регистрация, TimeProvider, CancellationToken, явные транзакции.
- Отдельная CookieAuthentication схема IdentityAdminCookie, policies IdentityAdminRead
  (обе роли), IdentityAdmin (только full administrator). Default Bearer scheme сохранена.
- HttpOnly + Secure + SameSite=Strict + Path=/, без Domain, cookie names с __Host- prefix.
  Auth cookie непостоянная, содержит защищённый ticket с user/session IDs и system role,
  без JWT/refresh/password/email. Framework Data Protection с постоянным key ring и
  application name Identity.Admin. Каждый admin request сверяет status/email/role,
  владельца session, revocation, idle/absolute expiry. Роль берётся из БД.
- Срок бездействия по умолчанию 30 минут; абсолютный максимум 8 часов. Sliding ticket
  отключён; DB touch не продлевает абсолютный срок. Validation выполняется в отдельном
  scope/транзакции, чтобы MVC use cases не получили устаревшие tracked entities.
- MVC Controllers/Admin/AdminAuthController, JSON/ProblemDetails без redirect:

| Метод | Endpoint | Результат |
|---|---|---|
| GET | /api/v1/admin/auth/csrf | 200 {token} + защищённая antiforgery cookie |
| POST | /api/v1/admin/auth/login | email/password + CSRF -> 200 safe profile + auth cookie |
| GET | /api/v1/admin/auth/me | 200 {id,email,displayName,role} |
| POST | /api/v1/admin/auth/logout | CSRF + cookie -> server revoke/delete cookie, 204 |

- Общий AdminSecurityFilter: HTTPS даже в Development, opt-in flag, ASP.NET antiforgery
  для всех unsafe admin методов, включая login/logout. Request token только X-CSRF-TOKEN,
  без form/query fallback. После login нужно заново запросить identity-bound CSRF token.
- Login использует существующие IP/account limits, password verification/rehash и общий
  конкурентно защищённый account lockout. Неверные credentials/inactive/locked -> generic401;
  правильный пароль без роли ->403/no cookie; нет/недействительна cookie ->401; ReadOnly
  write-policy ->403; CSRF ->400 csrf_invalid. CSP/no-referrer/nosniff/no-store/trace сохранены.
- SessionRevoker расширен: password change/reset/logout-all/deactivation отзывают также
  admin sessions в той же транзакции. CLI revoke/смена роли отзывают admin cookies через DB.
- PostgreSQL advisory transaction lock -> user row -> sessions. Guard последнего Active
  verified full administrator применяется к revoke/downgrade и существующей self-deactivation.
  Последняя возвращает409 last_administrator; ReadOnly не считается резервным full admin.
- Audit retention worker работает и при включённом только admin auth.

### CLI / конфигурация / документация

- Новый локальный executable **src/Identity.AdminCli** в solution: grant/revoke по точному
  UUID либо normalized exact email, optional role. Нет HTTP/hosted worker startup, паролей
  по умолчанию и credentials в аргументах. Database__ConnectionString через protected env.
  Идемпотентность, безопасные exit codes, атомарный audit; actor/granted_by null для OS operator.
- AdminAccess: Enabled=false по умолчанию, DataProtectionKeyPath, IdleTimeoutSeconds=1800,
  AbsoluteLifetimeSeconds=28800. ValidateOnStart: существующий абсолютный directory, limits.
  Примеры в appsettings.json/.env.example/deploy/production.env.example. Никаких production
  ключей/секретов не создано; пользовательские env/keys не изменялись.
- docs/admin-access.md: bootstrap/revoke, protocol, HTTPS/VPN, shared key ring/rotation,
  backup/restore, ограничения и recovery. README, api/architecture/security/deployment/
  production docs и CHANGELOG обновлены. Production Compose не включает admin автоматически.
- Secret guard дополнен Data Protection key paths/conventional XML key filenames/raw
  master-key payloads; .gitignore закрывает data-protection-keys.

Ключевые файлы: Domain/{Entities,Enums}; Application/Features/Admin,
Application/Abstractions/{Persistence/IAdminRepository,Security/IAdminAccessSettings},
Common/AdministratorGuard, SessionRevoker и DeactivateAccountHandler;
Infrastructure/Persistence/{Configurations,Repositories/AdminRepository,Migrations},
Security/AdminAccessOptions, Security/AdminSession/AdminCookieAuthentication;
Api/Controllers/Admin, Contracts/AdminAuthContracts, Configuration/AdminSecurityFilter,
Program и существующие security middleware; Identity.AdminCli/{Program,csproj};
tests/Identity.UnitTests/AdminDomainTests, IntegrationTests/AdminAccessTests,
PersistenceTests/RegistrationFixture; docs/admin-access.md и перечисленные docs/configs.

## 3. Решения

Clean Architecture и существующие API сохранены. Domain standalone; Application -> Domain;
Infrastructure -> Application + Domain; API и CLI -> Application + Infrastructure. ASP.NET
Cookie/Data Protection детали находятся в Infrastructure (shared Microsoft.AspNetCore.App
FrameworkReference); четыре избыточных PackageReference удалены после NU1510, их типы
предоставляет shared framework. Новых NuGet libraries, mediator или Identity user management
не добавлено. Контроллеры не получают DbContext/repositories, CQRS остаётся собственным.

Global administrators lock нужен из-за write-skew между двумя разными user rows. Он общий
у CLI и self-deactivation и всегда берётся перед user lock. Login/session validation не
берут global lock: они сериализуются на user row. Все проверки/изменения eligibility
должны сохранять этот порядок в будущем этапе 10; ad-hoc SQL не защищён Application guard.

Cookie и antiforgery отделены от пользовательского JWT protocol. Успешный login создаёт
новый session ID; native/BFF sessions не переиспользуются. CLI revoke роли не означает
блокировку обычного аккаунта. Admin flag не требует включённого Jwt/SMTP; CLI не загружает
эти keys и не запускает workers. Data Protection persistence не обещает encryption at rest:
OS ACL/encrypted storage/операторский supported protector обязательны по runbook.

Приглашения/disable metadata на этапе 8 — только требуемая Domain/schema подготовка,
без flow приглашений, писем, dashboard/read API, administrative user commands и frontend.
Политики проверены test-only controller, который не входит в production API.

## 4. Проверки

Команды выполнялись из корня, .NET SDK 10.0.402, PowerShell 7.6.5, Docker Linux:

```powershell
dotnet restore Identity.slnx
dotnet tool restore
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test tests/Identity.IntegrationTests --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AdminAccessTests|FullyQualifiedName~PersistenceTests' --logger 'trx;LogFilePrefix=stage8-admin'
dotnet test tests/Identity.IntegrationTests --configuration Release --no-build --no-restore --filter FullyQualifiedName~AdminAccessTests --logger 'trx;LogFilePrefix=stage8-admin-expanded'
dotnet format Identity.slnx --no-restore
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage8-full'
dotnet format Identity.slnx --no-restore --verify-no-changes
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
dotnet list Identity.slnx package --vulnerable --include-transitive
pwsh -NoProfile -File scripts/Test-Stage7Smoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
dotnet publish src/Identity.AdminCli --configuration Release --no-build --no-restore --output TestResults/admin-cli-publish
dotnet TestResults/admin-cli-publish/Identity.AdminCli.dll --help
git -c core.safecrlf=false diff --check
```

Migration generation: `dotnet ef migrations add AdminSecureAccess --project
src/Identity.Infrastructure --configuration Release --no-build` с design-only env.
Последние restore/build/format/EF/publish проверки exit0, сборка **0 warnings / 0 errors**.

- **366 passed, 0 failed, 0 skipped**: Unit **114**, API **45**, Integration **207**.
  Это прежние339 +27 новых проверок. Реальные PostgreSQL17/Mailpit/Testcontainers, без EF
  InMemory/SQLite. TRX stage8-full в TestResults тестовых проектов, общий log
  TestResults/stage8-full-tests.log (ignored).
- Admin suite18: actual CLI grant/login/me; Secure cookie attributes; CSRF missing/wrong
  browser/old anonymous token at logout; cookies на новом API host с тем же ring; logout replay;
  non-admin403; disabled/deactivated/missing credentials; revoked role; ReadOnly403;
  admin cookie/Bearer isolation; idle and absolute expiry via fake TimeProvider; parallel
  real CLI revoke processes retain one administrator; downgrade refusal; concurrent
  CLI revoke vs self-deactivation; password change/all-session revoke; cookie tamper/
  foreign session binding; role-change revocation/idempotence/audit; Invited without
  credentials rejected by both login paths and CLI; opt-in/key-path/HTTPS validation;
  brute-force/account limits and no secrets in captured logs/CLI output.
- Все4 migrations Up/Down/reapply/model drift на PostgreSQL проходят. Новая проверка
  upgrade с предыдущей схемы сохраняет user/status/hash/audit IDs и nullable legacy
  metadata. Старый legacy-audience migration test вставляет прежнюю SQL shape, не новые
  поля текущей EF model. EF pending changes нет; idempotent migration.sql сформирован.
- Промежуточный baseline --no-build до компиляции новой migration получил
  PendingModelChangesWarning, а первая сборка тестов выявила отсутствующий test DTO.
  DTO добавлен, миграция пересобрана, последующие целевые и полный прогоны зелёные.
  Начальный NU1510 устранён удалением redundant PackageReference после FrameworkReference.
- NuGet audit8 проектов: известных уязвимых packages по текущим sources не обнаружено.
- Docker smoke exit0: новый API image build, применение4 migrations, register -> Mailpit
  -> verify -> login -> refresh -> me -> logout -> failed refresh, плюс replay/restart/
  sessions/password/deactivation и headers/errors. Non-root и Docker health healthy.
  Admin flag в этом compatibility smoke выключен; новые admin HTTP сценарии проверены
  WebApplicationFactory с PostgreSQL и отдельными реальными CLI процессами. Лог:
  TestResults/stage8-docker-smoke.log. Все временные Compose containers/network/volume/
  env/keys удалены. Shared user environment не менялся.
- Secret guard self-tests проходят; реальный temporary untracked XML master-key sentinel
  отклонён с ненулевым exit без отражения значения в output, затем удалён. Повторный
  guard проходит. Публикация CLI в ignored TestResults и запуск --help успешны.
- git diff --check проходит. CI workflow не запускался удалённо; существующий CI включает
  новые projects/tests через solution. Frontend lint/test неприменимы: frontend — этап11.
  Production TLS/SMTP/VPN и browser E2E с frontend не запускались и не заявляются как passed.

## 5. Безопасность

Системная роль не может назначаться self-register/profile/API. Admin транспорт отделён
от Bearer, с DB validation на каждом запросе, немедленным отказом после role/session
revocation и status change. CSRF обязателен включая login/logout; Secure/HttpOnly/Strict,
HTTPS, no-store/no-referrer/CSP и лимиты не ослабляются в dev. Shared account lockout,
production password hashing и безопасный audit/logging повторно использованы.
Последний активный full administrator защищён атомарно, включая cross-flow race с
существующей self-deactivation. Смена/сброс пароля не оставляют admin cookies действующими.

Постоянные Data Protection keys отделены от JWT/AES, не выводятся и не добавляются в Git;
guard имеет positive/negative проверку. Raw passwords, auth/CSRF cookies и tokens не
логируются. Клиенту возвращается только безопасный admin profile и необходимый CSRF token.

## 6. Ограничения / блокеры

Блокеров этапа8 нет. Этапы9–12 не реализованы. Приглашения/disable/enable/admin session
management/dashboard/list/audit API и React UI пока отсутствуют. Без MFA admin API
допустим только за VPN/эквивалентным trusted perimeter. Этот perimeter, HTTPS ingress,
защищённый writable shared Data Protection volume и его encryption/backup задаёт оператор;
текущие Compose samples не открывают/не включают admin автоматически.

Admin session rows пока сохраняются после expiry/revoke; validation их отклоняет,
автоматический purge оставлен later hardening. CLI OS operator не имеет выдуманного
Identity actor ID: actor/granted_by null, target и trace сохраняются. Requests,
авторизованные до отзыва, могут завершиться; автономные downstream JWT имеют exp/skew lag.
Rate limits локальные для процесса, SMTP at-least-once и остальные v1 ограничения сохранены.
Downgrade при новых enum/audit данных требует явного data compatibility plan; миграция
не удаляет такие строки молча. После restore нужно отозвать также admin_sessions и
сверить восстановленные admin роли/статусы с security timeline.

Внешние deployment/accounts, registry/tag/commit, удалённый CI не выполнялись. На этом
этапе не обещаются frontend/browser E2E, production VPN/MFA или автоматическая encryption
at rest Data Protection ring. Спецификационных расхождений этапа8 не выявлено; единственное
расширение scope базового v1.0 прямо разрешено addendum и текущим запросом пользователя.

## 7. Следующий этап

Рекомендация: **9 — Read-only Admin API** (dashboard, users/details/sessions/audit,
server-side pagination/filter/sort, DTO projections, tests). Не начат; только после
отдельного подтверждения пользователя.
