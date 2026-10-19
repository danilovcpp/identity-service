# Отчёт по этапу 9

## 1. Этап

**9 — Read-only Admin API**, завершён 2026-10-19.
Прочитаны AGENTS.md, основной Identity_Codex_Spec.md, Identity_Admin_Codex_Addendum.md,
предыдущий IMPLEMENTATION_STATUS.md; изучены код и Git history. Рабочее дерево было
чистым. Этап8 подтверждён commit **0b8536a**, этап7 — c5f230d и прежними commits/tests.
Выполнен только этап9 дополнения (§5.2, §8). Этапы10–12 не начинались.

## 2. Реализовано

Пять GET endpoints внутри Identity.Api, с существующей политикой IdentityAdminRead:

| Endpoint | Результат |
|---|---|
| /api/v1/admin/dashboard | Все status counts, active sessions, zero-filled UTC trend30 дней |
| /api/v1/admin/users | Page/items/total, search/status/emailVerified, whitelist sort |
| /api/v1/admin/users/{userId} | Safe user details, включая Invited/Disabled/Deactivated |
| /api/v1/admin/users/{userId}/sessions | Страница удерживаемых native/BFF sessions, optional state |
| /api/v1/admin/audit-events | Страница security audit с user/actor/event/time filters |

- Application: GetDashboardQuery, ListUsersQuery, GetUserDetailsQuery, GetUserSessionsQuery,
  ListAuditEventsQuery; отдельные handlers, typed validators и DTO, явная регистрация DI.
  IAdminReadRepository — конкретная read-проекция для admin use cases, без generic repository.
- Pagination: page1+, pageSize25 по умолчанию, максимум100; проверка int offset overflow.
  Beyond-last page -> empty items + фактический total. Count/page — один RepeatableRead
  snapshot; отдельный тест вставляет строку с другого соединения между двумя SELECT.
- Users: search <=200, без control chars, literal substring ILIKE email/displayName;
  %, _ и backslash экранируются. Status только имена enum, emailVerified optionalbool.
  Sort только createdAt/email/status и asc/desc, с UUID tie-breaker. SQL values — параметры.
- Dashboard: взаимоисключающие user counts, active sessions = unrevoked + created<=now +
  absoluteExpiry>now из sessions, без admin cookie sessions. Trend включает сегодня и
  предыдущие29 UTC-дней, нулевые дни дополнены, группировка явно AT TIME ZONE UTC.
  Добавлены asOf/registrationTrendFrom/registrationTrendTo для явного периода измерения.
- Details: профиль/status/timestamps/disable metadata/lockout counters; без credentials,
  normalizedEmail, passwords/hashes, refresh/one-time tokens, outbox, entity graphs.
- Sessions: только указанный userId, существующий retention, createdAt DESC/id DESC;
  optional state active/revoked/expired, DTO без token/hash. Absent user ->404 user_not_found,
  существующий user без sessions ->empty200. Отзыв сессий не реализован в этапе9.
- Audit: userId соответствует legacy user_id OR target_user_id; actorUserId отдельно,
  eventType как ограниченное snake_case имя, from inclusive/to exclusive с обязательным
  explicit UTC offset и нормализацией UTC. Все filters AND, порядок occurredAt DESC/id DESC.
  Arbitrary metadata не выдаётся; CLI actor может быть null, как в этапе8.
- Infrastructure: AsNoTracking Select/GroupBy, асинхронные SQL, CancellationToken,
  DTO-only materialization, без SaveChanges/загрузки коллекций/N+1. ReadOnly и full admin
  используют одну cookie-policy, отдельные public Bearer API не менялись.
- OpenAPI содержит все5 routes, query parameters, safe DTO/page schemas.
- Новая миграция **20261019022952_AdminReadIndexes**:7 composite B-tree indexes, два прежних
  single-column audit indexes заменены prefix-compatible composites. Таблицы, данные,
  enum values и FK не изменены. Startup migrations не добавлены.

Ключевые файлы относительно корня:

- src/Identity.Application/Features/Admin/{Dashboard,Users,Audit,Common};
  Abstractions/Persistence/IAdminReadRepository.cs, Common/AdminUserNotFoundException.cs,
  DependencyInjection.cs.
- src/Identity.Infrastructure/Persistence/Repositories/AdminReadRepository.cs,
  Configurations/{User,Session,SecurityAuditEvent}Configuration.cs,
  Migrations/20261019022952_AdminReadIndexes{,.Designer}.cs и snapshot; DependencyInjection.cs.
- src/Identity.Api/Controllers/Admin/{AdminDashboard,AdminUsers,AdminAudit}Controller.cs,
  Middleware/ApiExceptionHandler.cs (404 user_not_found).
- tests/Identity.UnitTests/AdminReadValidationTests.cs;
  tests/Identity.IntegrationTests/AdminReadTests.cs, PersistenceTests.cs.
- docs/admin-read-api.md — полный контракт, filters/UTC/pagination/DTO, curl, SQL limits;
  README, CHANGELOG, docs/{admin-access,api,architecture,security,deployment}.md обновлены.
  .env.example содержит ссылку; новых options/secrets/services нет, AdminAccess settings прежние.

## 3. Решения

Clean Architecture/Controllers/собственный CQRS сохранены. Domain не изменён;
Application не получил EF/ASP.NET/Npgsql зависимости, контроллеры не получают DbContext/
репозитории. Новых NuGet dependencies и frontend code нет. Не добавлены stage10 commands,
приглашения или UI. Версия release1.0.0 сохранена; CHANGELOG описывает Unreleased v1.1.

RepeatableRead удерживает один MVCC snapshot только на время составного запроса и не
берёт FOR UPDATE locks. Страницы разных HTTP requests не являются единым snapshot:
при concurrent writes возможен сдвиг OFFSET, что явно описано. Запросы не меняют business
state; cookie validation отдельно обновляет last_seen_at в существующем scope/transaction.

Dashboard дополнен периодом/asOf согласно §1 addendum: To — следующая UTC midnight,
сегодняшний bucket частичный до inclusive asOf. TotalUsers — все сохранённые rows;
future-created test rows исключены из trend, но не из total. Native/BFF session metrics
не смешаны с admin transport sessions. UTC grouping не зависит от DB session timezone.

Контракты audit/sessions, не детализированные в §5.2, уточнены в docs/admin-read-api.md:
пагинация всех списков, half-open time range, legacy user/target matching, фиксированные
sorts, retained sessions вместо обещания архива. Пользовательские API не переопределены.

Миграция индексов использует обычное транзакционное создание: возможна блокировка writers
на больших таблицах, нужен операторский migration window. No pg_trgm/extension privilege
зависимости; arbitrary substring ILIKE и deep OFFSET могут сканировать большие таблицы.
Дополнительный search engine/keyset API не введены вне scope. DTO materialization и
bounded pages не выдаются за гарантию индексного поиска для произвольной подстроки.

## 4. Проверки

Выполнены из корня, .NET SDK10.0.402, PowerShell7.6.5, Docker Linux:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations add AdminReadIndexes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AdminReadTests|FullyQualifiedName~AdminReadValidationTests|FullyQualifiedName~PersistenceTests' --logger 'trx;LogFilePrefix=stage9-targeted'
dotnet format Identity.slnx --no-restore
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage9-full'
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

- Restore/build/format/EF checks exit0. Итоговая сборка **0 warnings, 0 errors**.
- **410 passed, 0 failed, 0 skipped**: Unit **133**, API **45**, Integration **232**.
  Прежние366 +44 новых:19 validator/UTC unit cases и25 PostgreSQL/API/query cases.
  Целевой прогон:19 unit +60 integration (admin reads + существующие persistence tests);
  отдельный ApiTests project не имел совпадений target filter, но все45 выполнены полным run.
- Реальный PostgreSQL17/Mailpit через Testcontainers; без EF InMemory/SQLite.
  Новые tests: empty DB, все5 statuses,30 days/zero buckets/UTC boundary при PostgreSQL
  America/New_York, strict session expiry/revocation, filters AND, literal ILIKE wildcard
  escaping/injection text, все6 combinations sort/direction, ties/page boundaries/empty
  beyond-last, legacy audit subject/admin actor+target, offset normalization и half-open
  ranges; malformed queries ->ProblemDetails, absent user404, отсутствие секретных полей,
  обе роли читают все5 routes, public Bearer/anonymous/revoked cookie denied, OpenAPI paths.
- AsNoTracking проверяется пустым ChangeTracker после query. Фиксированы SELECT counts:
  users2, details1, sessions3, audit2, dashboard3. Count/page consistency при concurrent
  insert проверена отдельным connection/interceptor. DTO SQL не обращается к credentials.
  На5000 пользователях все5 query calls суммарно **104.4ms** в финальном локальном run
  (целевой sample68.1ms), включая DB/DI; это не production SLA и не стабильный benchmark.
- Все5 migrations Up/Down/reapply прошли на PostgreSQL, indexes проверены. Существующие
  legacy upgrade/data preservation tests зелёные. EF pending model changes нет;
  idempotent migration.sql сформирован (ignored). Миграция только indexes, не новые rows.
- NuGet audit8 проектов: известных уязвимых packages по текущим sources не найдено.
- Docker compatibility smoke exit0: API image build, применение5 migrations, register ->
  Mailpit -> verify -> login -> refresh -> me -> logout -> failed refresh; также replay,
  restart, own sessions, password reset/change/deactivation, headers/error contract.
  Non-root/healthcheck healthy. Admin flag в этом smoke выключен; новые admin HTTP routes
  проверены WebApplicationFactory с PostgreSQL, не называются browser/container admin E2E.
  Временные Compose containers/network/volume/env/keys удалены.
- Secret guard self-tests/current tracked+untracked tree и git diff --check проходят.
  Remote CI, production TLS/VPN/SMTP и frontend browser E2E не запускались. Frontend
  lint/test неприменимы: frontend ещё не реализован, относится к этапу11.

Локальные ignored artifacts: TestResults/stage9-targeted.log, stage9-full-tests.log,
stage9-docker-smoke.log, stage9-nuget-audit.log; TRX stage9-full в TestResults тестовых
проектов. Секреты, generated SQL/logs/TRX не добавлены в Git.

## 5. Безопасность

Все read routes используют IdentityAdminRead с cookie scheme и текущей DB role/session
validation. ReadOnly разрешено чтение, новых mutations нет; существующий write-policy
тест этапа8 продолжает отклонять ReadOnly. Сохранены HTTPS, Secure/HttpOnly/Strict cookies,
CSRF для unsafe методов, no-store/CSP/no-referrer и audit/trace secrecy.

Размеры запросов/страниц ограничены; literal search экранирован, SQL values параметризованы,
sort identifiers не принимаются. Наивные datetime/неподдерживаемые enum/sort/overflowing
pagination отклоняются с константными field errors. Entity graphs, password/token hashes,
refresh/one-time tokens, SMTP/outbox payload и произвольная metadata не выбираются/не выдаются.
Логи используют прежние route templates, а не search/query/PII; negative input не отражается
в ProblemDetails. Данные списка/аудита доступны только валидной admin cookie.

## 6. Ограничения / блокеры

Блокеров этапа9 нет, обнаруженных build/test ошибок не осталось. Новых конфигурационных
ключей, зависимостей от внешних приложений и требований к секретам нет. AdminAccess остаётся
opt-in; постоянный key ring и VPN/эквивалентный trusted perimeter до MFA обязательны.

Этап10 mutations/invitation flow, этап11 frontend и этап12 integrated hardening/release
не реализованы. Read queries не меняют существующую retention: expired sessions/audit
показаны только пока они сохраняются. Admin-cookie session purge остаётся ограничением
этапа8. Глубокая offset pagination/substring search и exact totals могут быть дороги при
большом объёме; отдельные страницы не обещают общую согласованность во времени. Создание
индексов обычным DDL планируется оператором, миграция не запускается при API startup.

Production load/TLS/VPN/SMTP, удалённый CI и browser E2E не выполнялись; публикации, deployment,
Git commit/tag и внешних действий не было. Противоречий требованиям этапа9 не выявлено;
уточнения недетализированных DTO/filters/period приведены выше и в API documentation.

## 7. Следующий этап

Рекомендация: **10 — Administrative write commands**, приглашения/acceptance, reset,
disable/enable и session revoke с audit/transactional outbox/last-admin protection.
Не начат; выполнение только после отдельного запроса пользователя.
