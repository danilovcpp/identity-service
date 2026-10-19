# Отчёт по этапу 10

## 1. Этап

**10 — Administrative write commands**, завершён 2026-10-19.
Изучены AGENTS.md, Identity_Codex_Spec.md, Identity_Admin_Codex_Addendum.md целиком,
текущий код, Git history и предыдущий IMPLEMENTATION_STATUS.md. Дерево было чистым;
этап9 подтверждён commit **9cff58b**, этап8 — 0b8536a, этап7 — c5f230d.
Выполнен только этап10 дополнения (§3–5, §8). Этапы11/12 не начинались.
Новый frontend, страницы активации/reset и внешнее развёртывание не выполнялись.

## 2. Реализовано

### HTTP и Application

Все административные команды — MVC Controllers + собственный CQRS, cookie-политика
IdentityAdmin (только полный администратор), HTTPS, CSRF и существующие IP/account limits.
Actor/user-session IDs берутся из cookie. Приложение не принимает их от клиента в JSON.

| Метод | Endpoint | Ответ |
|---|---|---|
| POST | /api/v1/admin/users/invitations | 202, пустое тело |
| POST | /api/v1/admin/users/{userId}/invitations/resend | 202, пустое тело |
| POST | /api/v1/admin/users/{userId}/password-reset | 202, пустое тело |
| POST | /api/v1/admin/users/{userId}/disable | reason required, 204 |
| POST | /api/v1/admin/users/{userId}/enable | 204 |
| DELETE | /api/v1/admin/users/{userId}/sessions/{sessionId} | 204 |
| POST | /api/v1/admin/users/{userId}/sessions/revoke-all | 204 |
| POST | /api/v1/auth/invitations/accept | public token/newPassword JSON, 204 |

- Отдельные command/handler/validator slices: InviteUser, ResendInvitation,
  RequestAdminPasswordReset, DisableUser, EnableUser, RevokeUserSession,
  RevokeAllUserSessions, AcceptInvitation. Явная регистрация DI, TimeProvider,
  CancellationToken, явные транзакции. Контроллеры не получают DbContext/repositories.
- AdminWriteAuthorization повторно проверяет actor Active+verified, текущую роль и
  session owner/revocation/idle/absolute expiry внутри mutation transaction. Проверка
  middleware не является единственным разрешением на изменение.
- Invite: Invited без credentials + hash-only Invitation token + AES-GCM encrypted
  whole-email outbox + audit actor/target атомарно. Пароль администратором не назначается.
  Любой существующий email ->409 admin_email_conflict; конкурентная unique violation
  также переводится в409 без частичных записей. Public register сохраняет generic202.
- Resend только Invited без credentials; прежние invitation tokens инвалидируются,
  новый hash/token/outbox/audit создаются в той же транзакции. SMTP-доставка at-least-once:
  старые queued emails могут прийти, но их tokens уже непригодны.
- Public accept: purpose/ownership/expiry/single-use/status/отсутствие credential,
  user -> token locks, начальный PBKDF2 credential и verified Active атомарно; другие
  invitations инвалидируются. Пароль выбирает получатель, прежняя NFKC/password policy.
  Автоматический login и grant admin-role не выполняются. Повторный/просроченный/неподходящий
  token/state/credential -> одинаковый400 invalid_invitation_token.
- Admin reset переиспользует30-minute token/encrypted email/public reset API. Только
  Active+verified с credentials (временный brute-force lock допустим). Запрос не меняет
  пароль/сессии; успешное применение reset отзывает оба типа сессий существующим lifecycle.
- Disable сохраняет исходный Active/PendingVerification/Invited, actor/reason/time,
  отзывает native/BFF refresh families и admin cookies, инвалидирует verification/reset/
  invitation tokens. Enable восстанавливает исходный status, не подтверждает email,
  не создаёт credential и не оживляет старые tokens/sessions. Deactivated не реанимируется.
- Повторный disable/enable в уже целевом допустимом состоянии ->идемпотентный204;
  original disable reason/audit не переписывается. Enable не сбрасывает отдельный
  brute-force lockout. Для legacy Disabled без prior status: Active только при verified,
  иначе PendingVerification. При enable disable metadata очищается.
- Последний Active+verified полный администратор защищён от disable, CLI revoke/downgrade
  и self-deactivate общей advisory transaction lock. User locks берутся по sorted IDs
  перед session/token locks. ReadOnly не считается резервным полным администратором.
- Targeted revoke работает с существующей native/BFF sessions таблицей из stage9,
  проверяет owner; отсутствующая/чужая session404, повторный revoke204. Revoke-all
  отзывает native/BFF и admin sessions, включая текущую при self-target. История tokens
  сохранена для replay detection; новые токены при revoke не выдаются.
- Audit actor/target/time/correlation/whitelisted action сохраняется с commit. Добавлены
  invitation_accepted и admin_revoked_all_sessions; используются существующие имена
  admin_invited_user/admin_resent_invitation/admin_requested_password_reset/
  admin_disabled_user/admin_enabled_user/admin_revoked_session. Free-text reason в audit
  metadata не помещается, хранится на user во время блокировки. Секретов/ссылок нет.

### Infrastructure / Domain / конфигурация

- InvitationRepository: purpose-specific owner lookup/row lock, атомарная invalidation,
  включая future-issued links без нарушения timestamp constraints. InvitationIssuer
  повторно используется invite/resend; handlers не вызывают друг друга.
- Outbox worker поддерживает новый тип invitation; сохраняет encrypted payload при
  SMTP failure/retry, очищает после успеха. Key ring/startup validation/maintenance
  учитывают Invitations, даже когда registration/reset/JWT отключены.
- Invitations.Enabled=false по умолчанию, LifetimeSeconds=259200 (72h), допустимо60–259200.
  Email.InvitationUrlTemplate — фиксированный URL только с #token={token}. Production:
  outbox worker, SMTP TLS, корректные отдельные AES keys и non-dev key IDs обязательны.
  Admin password reset при AdminAccess+PasswordReset требует fragment-only reset URL.
- appsettings/.env.example/compose/production.env.example обновлены. Dev Compose invitation
  flag независим от AdminAccess/TLS/key-ring setup; существующий plain HTTP dev bootstrap
  не превращён в небезопасную cookie-админку. Никакие реальные env/keys пользователя не менялись.
- Новая additive migration **20261019024608_AdminWriteLifecycle**: nullable
  users.status_before_disable + check; revoke reasons AccountDisabled=7,
  AdministratorRevoked=8; audit whitelist. Старые enum values/rows/FK/indexes сохранены,
  prior state для legacy rows не выдумывается. Startup migrations не добавлены.
- docs/admin-write-api.md: все contracts/curl, настройки, idempotence/conflicts,
  transaction/locking decisions, SMTP semantics, JWT lag, release boundaries.
  README/CHANGELOG, admin-access/read/api/architecture/security/deployment/production docs
  обновлены; smoke env фиксирует Invitations=false для прежних compatibility сценариев.

Ключевые файлы относительно корня:

- Domain/Entities/{User,SecurityAuditEvent}, Enums/{SessionRevokeReason,SecurityEventType}.
- Application/Features/Admin/Users/{InviteUser,ResendInvitation,RequestPasswordReset,
  DisableUser,EnableUser,RevokeUserSession,RevokeAllUserSessions}; Features/Auth/AcceptInvitation;
  Features/Admin/Common/{AdminWriteAuthorization,AdminWriteValidation,InvitationIssuer};
  Abstractions/{Security/IInvitationSettings,Notifications/IInvitationEmailFactory,
  Persistence/IInvitationRepository}, Common/AdminWriteExceptions, DependencyInjection.
- Infrastructure/Email/{InvitationOptions,InvitationEmailFactory,EmailOptions,
  EmailDependencyInjection,EmailOutboxProcessor}, Security/OutboxKeyRing,
  Maintenance/SecurityMaintenanceWorker, Persistence/Repositories/InvitationRepository,
  entity configurations, Migrations/AdminWriteLifecycle + Designer/snapshot.
- Api/Controllers/Admin/AdminUserCommandsController, AuthController,
  Contracts/AdminUserRequests, Middleware/ApiExceptionHandler, appsettings.json.
- tests/Identity.IntegrationTests/AdminWriteTests, обновлённые AdminAccessTests/
  AdminReadTests/PersistenceTests; UnitTests/AdminWriteValidationTests и
  RegistrationSecurityTests (новый options constructor argument).
- docs/admin-write-api.md и перечисленные docs/config samples,
  scripts/Test-Stage3Smoke.ps1, compose.yaml.

## 3. Решения

Clean Architecture/Controllers/custom CQRS сохранены. Domain standalone; Application ->
Domain; Infrastructure -> Application+Domain; API -> Application+Infrastructure. Новых
NuGet libraries, Identity user management, mediator, generic repository или внешней
business-зависимости нет. Существующие REST login/refresh/register/profile APIs не ломались.
Версия release1.0.0 сохранена, дополнение v1.1 описано как Unreleased.

Одна глобальная administrators lock для административных writes — сознательный простой
MVP-компромисс: корректность actor/target/last-admin transitions вместе с CLI важнее
максимального throughput административных операций. Публичные user lifecycle операции
продолжают использовать user-first row locks. Нет locks вокруг SMTP: письмо лишь queued
транзакционно, network IO выполняет существующий outbox worker после commit.

Для точного enable добавлено поле prior status, а не предположение Active для всех.
Deactivated ->conflict; duplicate invitation ->private admin409; existing disabled/active
state operations идемпотентны. Targeted session endpoint соответствует native/BFF DTO
этапа9, bulk revoke включает admin transport. Решения/контракты явно описаны в документации.

Invitation feature flag отдельный: admin read/login deployments не требуют внезапно SMTP
и новых templates. Production templates строже для admin reset, как требует addendum.
SPA activation/reset pages не подменены server-side заглушками: это этап11. В этапе10
Mailpit ->API acceptance выполняется доверенным клиентом. HTTP202 не обещает доставку.

## 4. Проверки

Выполнены из корня, .NET SDK10.0.402, PowerShell7.6.5, Docker Linux:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations add AdminWriteLifecycle --project src/Identity.Infrastructure --configuration Release --no-build
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AdminWriteTests|FullyQualifiedName~AdminWriteValidationTests|FullyQualifiedName~PersistenceTests' --logger 'trx;LogFilePrefix=stage10-targeted'
dotnet test Identity.slnx --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AdminWriteTests|FullyQualifiedName~AdminWriteValidationTests|FullyQualifiedName~PersistenceTests' --logger 'trx;LogFilePrefix=stage10-targeted-fixed'
dotnet format Identity.slnx --no-restore
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage10-full'
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
dotnet list Identity.slnx package --vulnerable --include-transitive
pwsh -NoProfile -File scripts/Test-Stage7Smoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

- Итоговые restore/build/format/EF checks exit0. Build: **0 warnings / 0 errors**.
- **470 passed, 0 failed, 0 skipped**: Unit **147**, API **45**, Integration **278**.
  Это прежние410 +60 новых:14 unit validation/domain/template cases и46 PostgreSQL/API/
  Mailpit/transaction/concurrency cases. EF InMemory/SQLite не использовались.
- Проверены Mailpit invitation ->public accept ->login/me, hash-only72h token, отсутствие
  credentials до acceptance и отсутствие роли после; secret-free response/audit/logging;
  invalidated/resend/expiry/reuse, неправильный purpose и существующий credential;
  duplicate invite race, принудительно синхронизированная регистрация между lookup/save
  (admin409/registration202 без partial data), concurrent acceptance/resend;
  сохранение Active/Pending/Invited при disable/enable; blocked login/refresh/Identity JWT
  и admin cookie; last-admin concurrent self-disable; disable vs refresh; targeted/bulk
  owner/revocation semantics; Mailpit admin reset ->single use ->all-session revoke;
  все7 writes ReadOnly403 и missing CSRF400; recheck actor role/session/status/expiry
  внутри handler даже после старого cookie authorization; все8 use cases rollback.
- Rollback сравнивает безопасный SHA256 полного DB snapshot (включая admin revocation,
  исключая отдельный auth last_seen touch); raw hashes/ciphertext не печатаются assertion.
  Invitation-only host (без registration/JWT/reset) загружает ключи; SMTP failure
  сохраняет encrypted payload и machine error. Invalid lifetime/URL config/disabled flag
  проверены. Недопустимые startup options могут агрегироваться .NET StartupValidator.
- Первый targeted run: worker отклонил новый invitation type, Mailpit получил0 писем.
  Исправлен whitelist EmailOutboxProcessor; Mailpit delivery/acceptance в полном run зелёные.
  Второй expanded run выявил слишком узкое ожидание теста: AggregateException с двумя
  OptionsValidationException вместо одиночной TTL ошибки. Assertion уточнён, отказ startup
  не ослаблен. Итоговый полный run прошёл; промежуточные неуспешные проверки не скрыты.
- Все6 migrations Up/Down/reapply, constraints и legacy data-preservation tests прошли
  на PostgreSQL17. EF pending model changes нет; idempotent migration.sql сформирован.
- NuGet audit8 проектов: известных уязвимых packages по текущим sources не обнаружено.
- Docker compatibility smoke exit0: build нового non-root API image, применение всех6
  migrations, register ->Mailpit ->verify ->login ->refresh ->me ->logout ->failed refresh,
  плюс replay/restart/sessions/password/reset/deactivation и headers/errors. Docker health
  healthy. Admin/invitations выключены в compatibility smoke; новые admin/invitation API
  отдельно проверены WebApplicationFactory + реальный PostgreSQL/Mailpit. Это не browser
  frontend E2E. Временные containers/network/volume/env/keys полностью удалены.
- Secret guard self-tests/current tracked+untracked tree и git diff --check проходят.
  Frontend lint/test неприменимы до этапа11. Remote GitHub CI, production TLS/VPN/SMTP,
  MFA и внешний deployment не запускались и не заявляются passed.

Ignored artifacts: TestResults/stage10-targeted.log, stage10-targeted-fixed.log,
stage10-full-tests.log, stage10-docker-smoke.log, stage10-nuget-audit.log;
TRX stage10-full в TestResults тестовых проектов. Секреты/generated SQL/logs/TRX
не добавлены в Git. Commit/tag/registry publication не выполнялись.

## 5. Безопасность

Нет выдачи пароля/token/hash администратору, нет установки произвольного пароля через
admin API. Invitation подтверждает email через одноразовый случайный token, хранит только
SHA256, письмо — AES256GCM outbox; production SMTP требует TLS. URL token только fragment
для новых invitation/admin reset flows. Никаких user-controlled redirects.

Backend denies ReadOnly, requires cookie+CSRF и повторно проверяет полномочия в транзакции.
Общая блокировка защищает последнего администратора и конкурентные actor/target операции.
Disable отзывает обе session transports/refresh chains и старые одноразовые ссылки;
enable не является email verification, recovery token или session resurrection.
Audit/metrics содержат whitelisted event types/IDs/time/correlation без passwords/tokens/
links/reason text. Существующая crypto и key storage архитектура повторно использована.

## 6. Ограничения / блокеры

Блокеров этапа10 нет. Frontend и страницы /account/activate/reset-password — этап11,
production integrated UI/E2E/hardening/release — этап12, не реализованы. Invitations opt-in,
готовность фиксированных web templates и VPN/trusted ingress/DP/AES/SMTP secrets задаёт
оператор. До MFA admin не публикуется свободно в интернет. Старые emails могут доставляться
после resend/block, но их tokens недействительны; SMTP остаётся at-least-once.

Targeted revoke относится к native/BFF family из stage9; revoke-all дополнительно включает
admin sessions. Автономный JWT в стороннем API может жить до exp (+skew); глобального revoke
обещания нет. Rate limits локальные/общие с текущими AuthRateLimits; все admin writes
сериализованы global lock (MVP throughput tradeoff). Session/audit retention не менялась;
admin session purge остаётся прежним ограничением. Уже completed requests не отменяются.

Downgrade после новых events/revoke reasons требует data compatibility plan; миграция не
удаляет несовместимые rows молча. Legacy Disabled без prior status восстанавливает verified
Active или unverified Pending, никогда не подтверждает email автоматически. Фактические
production TLS/SMTP/VPN/load/backup rehearsal и удалённый CI не выполнялись.

Противоречий требованиям этапа10 не выявлено; не детализированные idempotence/legacy state/
transport choices явно зафиксированы в docs/admin-write-api.md. Внешние продукты, tenants,
бизнес-роли или зависимости не добавлены. Следующие этапы не реализовывались заранее.

## 7. Следующий этап

Рекомендация: **11 — React Admin Web** (React/TS/Vite/Ant Design, generated API types,
secure centralized cookie/CSRF client, admin/public account pages, component/Playwright
checks). Не начат; только после отдельного запроса пользователя.
