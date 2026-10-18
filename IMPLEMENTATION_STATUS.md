# Отчёт по этапу 4

Дата: 2026-10-19. Требования: AGENTS.md и Identity_Codex_Spec.md (§5.3–5.8, §6.2–6.3,
HTTP contracts §7, этап 4 §10, формат отчёта §13).

## 1. Этап

**4 — Refresh rotation, Logout, Sessions. Завершён.**

Рабочее дерево до изменений было чистым. Этапы 0–3 подтверждены кодом и Git:
`d7c4f94`, `8d901d7`, `647ae4c`, `4f64b57`. Использованы существующие Domain,
CQRS dispatchers, PostgreSQL UoW/row locks, PBKDF2, JWT/JWKS и конфигурация клиентов.
Этап 5 не начат; profile/password lifecycle API не добавлены.

## 2. Реализовано

- `Application/Features/Auth/Refresh/`: команда, validator, handler. Строгая rotation
  consumed/replaced chain, фиксированные client/audience, clipping к absolute expiry,
  fresh token state после row locks; replay отзывает всё семейство и фиксирует audit
  до generic 401. Unknown/malformed/expired/revoked/неподходящее состояние — одна ошибка.
- `Features/Auth/Logout/`, `LogoutAll/`, `Features/Sessions/GetSessions/`, `RevokeSession/`:
  public идемпотентный logout, Bearer logout-all, собственные active sessions/isCurrent,
  отзыв своей сессии, одинаковый 404 для чужой/отсутствующей.
- `Application/Common/TokenPairResult.cs`: общий login/refresh response без изменения
  существующего JSON контракта; secret-bearing типы — class, без record ToString.
  `SessionAuthorization.cs`, `SessionExceptions.cs`, `OpaqueTokenText.cs`: прикладные
  guards/errors/ограничения token input. Явные регистрации CQRS в AddApplication.
- `Application/Abstractions/Persistence/ISessionRepository.cs` и
  `Infrastructure/Persistence/Repositories/SessionRepository.cs`: immutable owner lookup,
  row locks, tracked token после блокировок, транзакционные bulk revoke и AsNoTracking projection.
- `Domain/Entities/Session.cs`: immutable audience binding при создании. LoginHandler
  сохраняет audience; ILoginRepository/LoginRepository/JwtAuthentication проверяют
  совпадение с session binding на собственных защищённых endpoints.
- `Api/Controllers/AuthController.cs`, `SessionsController.cs`, `ApiController.cs`,
  `Contracts/RefreshTokenRequest.cs`, `Middleware/ApiExceptionHandler.cs`: пять endpoints
  этапа, trusted sub/sid из claims, no-store, generic ProblemDetails. Controllers
  не получают DbContext/repositories. Refresh/logout наследуют IP/body limits AuthController.
- `Infrastructure/Maintenance/`: SessionCleanupOptions/Processor/Worker, bounded family
  batches с SKIP LOCKED, retention до absolute expiry + buffer, атомарное удаление family/session.
  Options/DI/appsettings, Compose и .env.example обновлены.
- `Persistence/Migrations/20261018221422_SessionAudienceBinding.cs`, designer/snapshot,
  SessionConfiguration/RefreshTokenConfiguration: audience/check/index и NO ACTION self-FK.
- `tests/Identity.IntegrationTests/SessionTests.cs`, `SessionCleanupTests.cs`,
  `SecretLogCapture.cs`, расширения PersistenceTests/RegistrationFixture;
  `UnitTests/SessionSecurityTests.cs`, API tests и адаптация Domain/Login tests:
  **55 дополнительных тестовых случаев** к 182 тестам этапа 3.
- README, docs/api.md, architecture.md, security.md, deployment.md обновлены.
  `scripts/Test-Stage4Smoke.ps1` использует общий изолированный bootstrap/cleanup
  Test-Stage3Smoke.ps1; прежний запуск stage 3 без параметра сохранён.

## 3. Решения

- Clean Architecture, ASP.NET Controllers и собственный CQRS сохранены. Новых
  NuGet-зависимостей нет; MediatR/generic repository/Identity user management не добавлены.
- Общий порядок всех мутаций: **user FOR UPDATE → session FOR UPDATE → token FOR UPDATE**.
  Lookup до транзакции возвращает только immutable IDs, не tracked token state.
  Это предотвращает stale consumption при ожидании конкурентного commit и согласует
  refresh/logout/revoke/logout-all с login. Мутации разных сессий аккаунта сериализованы
  также по user; выбран простой строгий механизм для небольшого сервиса.
- Rotation, lastUsedAt, новая строка, consumed/replaced и audit — одна транзакция.
  Revoke token rows через ExecuteUpdate использует ту же транзакцию; tracked token
  после bulk update не изменяется. Ошибка до commit откатывает всё.
- Повтор consumed token, даже после его собственного expiry, отзывает всю session,
  включая новый токен победителя конкурентного refresh. Grace window нет. Потеря ответа
  после успешного commit не восстанавливает старый token: его повтор — replay; нужен login.
- **Дополнение схемы §5.3:** sessions.audience добавлена для обязательной привязки §6.2.
  Значение фиксируется при login, не подставляется заново при refresh. Changed/disabled
  client не может выдать пару с другой audience. Дополнительные JSON selectors игнорируются.
- **Совместимость данных:** старая схема не сохраняла исходную audience, поэтому
  миграция оставляет NULL; legacy sessions требуют нового login. Их токены не получают
  inferred binding. Identity JWT guard также отвергает NULL; старые access JWT во внешних
  API действуют до exp (+skew). Данные не удаляются миграцией; logout по старому refresh
  и logout-all из новой сессии позволяют их отозвать. Это явно описано в deployment docs.
- Manual revoke использует audit event logout и session reason UserRevoked: отдельного
  session_revoked event спецификация не определяет. Повтор public logout не создаёт audit-дублей.
- История всех токенов сохраняется до absoluteExpiresAt + default 86400 секунд, даже
  после раннего logout. Cleanup удаляет целые семьи с self-FK NO ACTION (проверка после
  SQL statement), сохраняя same-session replacement FK. Batch ограничивает число семейств
  (25, диапазон 1–100), не количество token rows внутри одного семейства: большая семья
  удаляется атомарно и может занять больше времени. Poll 300 секунд, retention 1 час–7 дней.
- Worker включён при Jwt:Enabled и SessionCleanup:Enabled; health-only bootstrap без
  JWT не начинает maintenance IO. Retention audit/outbox/one-time tokens в этап 4 не добавлен.

## 4. Проверки

Из корня репозитория выполнены:

```powershell
dotnet restore Identity.slnx
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations add SessionAudienceBinding --project src/Identity.Infrastructure --configuration Release
dotnet format Identity.slnx --no-restore
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger "trx;LogFilePrefix=stage4"
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
dotnet list Identity.slnx package --vulnerable --include-transitive
pwsh -NoProfile -File scripts/Test-Stage4Smoke.ps1
git -c core.safecrlf=false diff --check
```

Результаты:

- Restore/tool restore, format и Release build успешны; **0 warnings, 0 errors**.
- **237 passed, 0 failed, 0 skipped:** Unit 82, API 18, Integration 137. Integration —
  PostgreSQL 17/Mailpit/Testcontainers, без InMemory/SQLite. Предыдущие этапы проходят.
- Rotation: последовательная цепочка, 2/4 конкурентных refresh → одна успешная rotation,
  остальные replay с отзывом победителя; replay expired consumed token; absolute expiry,
  disabled/deactivated/locked user, revoked token, changed/disabled client, legacy NULL,
  spoofed JSON selectors, restart persistence.
- Logout/logout-all/revoke: idempotency, ownership/foreign 404, изоляция foreign user,
  current-session JWT rejection, гонки refresh/revoke для всех трёх операций.
- Транзакции: сбой до commit rotation, после реального commit (потеря ответа), rollback
  replay revocation, rollback logout/logout-all/revoke и rollback cleanup после bulk DELETE.
- Cleanup: active/revoked history retention, точная временная граница, bounded batches,
  параллельные workers, SKIP LOCKED, BackgroundService в работающем API. Startup validation
  отклоняет неверные cleanup options. В собранных логах нет raw password/access/refresh.
- Migration Up/Down/reapply и upgrade старой схемы с данными проверены на PostgreSQL.
  Snapshot: **No changes have been made to the model since the last migration**.
  Idempotent migration.sql сгенерирован (ignored локальный артефакт).
- NuGet audit не выявил известных уязвимостей; diff check чист.
- Docker smoke: config --quiet, отдельные PostgreSQL/Mailpit, применение обеих миграций
  через EF database update, image restore/publish/build, non-root API, register 202 →
  email → verify 204 → login/JWKS → restart → rotation → restart → rotation → replay 401
  → отказ latest refresh/JWT → own sessions/revoke → logout-all → login/logout/repeat
  → failed refresh. PostgreSQL: 4 sessions, 6 refresh rows, все отозваны, audience binding
  сохранён, один replay audit, consumed history сохранена. Cleanup собственных временных
  контейнеров/network/volume/env/key files выполнен; внешние ресурсы не затронуты.
- Локальный sample одного полного прогона: **5 refresh = 70.2 ms total / 14.0 ms mean**;
  login с PBKDF2/signing/PostgreSQL — **535.6 ms**. Это latency samples, не production SLA
  и не полноценный нагрузочный тест целевой машины.

## 5. Безопасность

Закрыты double rotation, replay с сохранённой историей, race refresh/revoke, partial
commit состояния семейства, смена client/audience при refresh, IDOR в sessions API,
продление за абсолютный срок, преждевременная потеря evidence и утечка raw refresh в БД/логи.
JWT/refresh не выдаются при logout. Owner определяется trusted sub, не query/body.
Все секреты временных проверок случайные; committed private keys/SMTP secrets нет.

Ограничения: параллельный refresh клиента отзывает также победителя; требуется сериализация.
После потери committed ответа — новый login. Внешний API принимает JWT до exp (+skew)
независимо от локального session revoke. Rate limits локальны экземпляру, DB locks общие.
TLS/proxy hardening, дополнительная observability и password lifecycle остаются следующим этапам.

## 6. Ограничения/блокеры

Блокеров этапа 4 нет. Применимые build/test/migration/Docker проверки выполнены.
Перед обновлением нужна новая миграция; legacy sessions потребуют повторного login.
Большое семейство удаляется целиком, batch не ограничивает его token rows; параметры
maintenance нужно выбирать под реальный поток. Production load test не выполнялся.
Profile/password/deactivation HTTP operations не добавлены. Compose остаётся локальным
Development окружением. Внешнее развёртывание и Git commit не выполнялись.

## 7. Следующий этап

**5 — Profile и password lifecycle**: профиль, смена/сброс пароля и деактивация с
отзывом всех сессий. Только рекомендация; выполнение — после отдельного запроса.
