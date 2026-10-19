# Запуск и конфигурация Identity 1.0.0

Этап10: [параметры invitations/admin writes](admin-write-api.md). Новая шестая миграция
AdminWriteLifecycle, Invitations.Enabled=false по умолчанию. Включение требует fragment-only
template, SMTP/AES keys и outbox worker в production. AdminAccess остаётся отдельным opt-in.

Этап9 добавляет [read-only admin API](admin-read-api.md) с теми же AdminAccess settings,
без новых secrets/services. Примените пятую миграцию AdminReadIndexes отдельно; обычное
создание индексов может блокировать writers, для больших таблиц запланируйте окно.

Для дополнения v1.1, этап 8, см. [admin access](admin-access.md): explicit feature flag,
durable Data Protection, CLI grant/revoke и HTTPS даже локально. Admin UI пока отсутствует.

Для новой production установки используйте отдельный [runbook](production.md),
`deploy/compose.production.yaml` и `deploy/production.env.example`. Ниже — полный
справочник SDK/options/maintenance; быстрый dev запуск — [README](../README.md).
Три существующие миграции сохранены, новой схемы в этапе 7 нет.

## Этап 6: транспорт, retention и наблюдаемость

Перед обновлением примените третью миграцию `20261019012922_SecurityMaintenanceIndex`
обычной EF deployment-командой. Она добавляет partial index processed_at к outbox;
данные не удаляет. API не выполняет migration автоматически.

В production HTTPS обязателен для business API; HTTP получает 400 без redirect,
health/live и ready допускают внутренний HTTP. На публичном edge отвергайте HTTP
**до** передачи чувствительного тела приложению. Health/readiness ограничьте сетью;
их доступность не должна раскрывать детали PostgreSQL. HSTS 365 дней, без preload/
includeSubDomains; заголовки nosniff/no-referrer и correlation сохраняются на ошибках.
Dev Compose остаётся loopback HTTP и не является production topology.

При TLS termination задайте в окружении API:

```text
AllowedHosts=identity.example
TransportSecurity__ForwardedHeadersEnabled=true
TransportSecurity__KnownProxies__0=10.0.0.10
TransportSecurity__ForwardLimit=1
```

Адрес — пример: укажите фактический **exact IP** своего proxy (до 16 адресов,
IPv4/IPv6, включая корректную форму IPv4-mapped при её использовании). CIDR и
trust-all не поддерживаются. ForwardLimit 1–5, по умолчанию 1. Proxy должен заменить
недоверенные входные headers, передавать симметричные X-Forwarded-For и X-Forwarded-Proto;
X-Forwarded-Host не используется. Backend ограничьте доступом только от proxy.
При выключенной опции forwarded headers игнорируются. Пустая allowlist при включении,
0.0.0.0/::, wildcard AllowedHosts в production и автоматический trust-all switch
ASPNETCORE_FORWARDEDHEADERS_ENABLED/FORWARDEDHEADERS_ENABLED блокируют startup.
Механизм опирается на [ASP.NET Core forwarding middleware](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
Rate limiting использует IP после доверенного forwarding. На edge нужны суммарные
лимиты между replicas: встроенные лимиты локальны процессу.

| Options/env | Default / пределы |
| --- | --- |
| `SecurityMaintenance__Enabled` | true; Compose SECURITY_MAINTENANCE_ENABLED |
| `SecurityMaintenance__BatchSize` | 100 **на каждую из трёх таблиц**, 1–1000 |
| `SecurityMaintenance__PollSeconds` | 300, 1–3600 |
| `SecurityMaintenance__AuditRetentionDays` | 90, 1–3650; Compose AUDIT_RETENTION_DAYS |
| `SecurityMaintenance__TokenRetentionDays` | 1 после expiry, 1–30 |
| `SecurityMaintenance__ProcessedOutboxRetentionDays` | 7 после success, 1–365 |

Worker запускается только если maintenance и хотя бы одна JWT/registration/reset/
outbox функция включены. Bootstrap с выключенными функциями не делает maintenance IO.
Одна итерация удаляет максимум 3×BatchSize записей, с фиксированной задержкой между
итерациями. Границы строгие (< cutoff); заблокированные записи пропускаются.
Сбой до commit откатывает все три batches. Audit не хранит email/IP/password/token,
но содержит user UUID; политика 90 дней ограничивает его хранение. Backups имеют
отдельную операторскую retention-политику. Пользовательские PII/credentials не удаляются.
Pending/failed encrypted outbox остаётся для доставки/операторского разбора; AES keys
нужно сохранять до обработки backlog. Refresh history очищается только прежним
SessionCleanupWorker. Очистку и последнюю успешную выборку gauges нужно мониторить.

Встроенные `System.Diagnostics.Metrics` meters (exporter/collector не устанавливается):

| Meter / instrument | Значение / labels |
| --- | --- |
| `Identity.Auth` / `identity.auth.events` | Counter committed audit events; только `event` из whitelist (login_success/login_failed/refresh_replay_detected и др.) |
| `Identity.Auth` / `identity.auth.sessions.active` | Sampled DB-сессии Active users: unrevoked, audience bound, created <= now < absolute expiry; без labels |
| `Identity.Auth` / `identity.auth.sessions.sampled_at` | Unix seconds последней успешной выборки; 0 до первой/при выключенном worker |
| `Identity.Http` / `identity.http.requests`, `identity.http.duration` | Counter и histogram ms; route **template** и status, без raw URL/ID/query |
| `Identity.Email` / `identity.email.outbox.pending`, `identity.email.outbox.failed` | Backlog gauges, существующий email worker |
| `Identity.Email` / `identity.email.delivered`, `identity.email.delivery_failures` | Delivery counters без PII |

Auth counters per-process и best-effort: rollback не публикуется, crash после DB commit
до публикации может потерять measurement. Audit остаётся источником истины.
Sessions gauge не применяет текущую client configuration и временный login-lockout;
он показывает DB lifecycle, а не число JWT, принятых конкретным consumer.
Если maintenance выключен/сломался, gauge устаревает: проверяйте sampled_at.
Каждая replica видит глобальные DB counts: их нельзя суммировать между replicas.

Безопасные logs содержат event type/trace либо route template/status/duration.
Framework URL/auth/model-binding/EF/Npgsql diagnostics подавлены, чтобы не раскрывать
сырой вход и DB errors; ошибки приложения логируют тип и trace без exception object.
Не включайте request/response body logging, SMTP protocol logging, IdentityModel PII
или raw proxy access logs. При подключении стороннего exporter явно исключите raw
URL/query/header/body/user-agent и SQL; API очищает стандартные URL/agent tags своей
request Activity перед завершением. Security metrics не имеют user/client/trace labels.

Локальные проверки: `pwsh -NoProfile -File scripts/Test-Stage6Smoke.ps1`, полный
`dotnet test` (PostgreSQL/Mailpit). Test SecurityObservabilityTests выводит пять локальных
login/refresh samples с production PBKDF2; это не production SLA и не saturation benchmark.

## Обновление до этапа 5

Новых migrations нет: остаются InitialIdentity и SessionAudienceBinding. Добавьте
в существующий Compose `.env` `RESET_PASSWORD_URL_TEMPLATE` из `.env.example`:
`http://localhost:3000/reset#token={token}`. Compose явно включает PasswordReset;
SDK bootstrap оставляет его выключенным. Страница reset принадлежит оператору/BFF
и не поставляется backend. Production требует фиксированный HTTPS template.

PasswordReset независим от Registration: recovery можно включить при закрытой
публичной регистрации. При включении нужны валидные Email/SMTP и AES key ring;
production также требует Outbox:Enabled, SMTP TLS и ключи без dev-/test- IDs.
Worker-only конфигурация требует транспорт/key ring, но не URL templates:
письма уже содержат зашифрованные ссылки. При выключенном reset forgot даёт generic
202 без письма, reset — generic 400. Change/profile/deactivate отдельного флага не имеют.

## Обновление до этапа 4 и cleanup refresh history

Перед запуском новой версии примените миграцию
`20261018221422_SessionAudienceBinding` обычной отдельной EF deployment-командой.
Она добавляет nullable sessions.audience (<=300), check constraint, индекс
ix_sessions_expiry и меняет self-FK replacement на NO ACTION для атомарного удаления
целой истёкшей цепочки одним DELETE. Composite FK по token/session сохраняется.

**Сессии этапа 3 требуют нового login.** Их исходная audience не была сохранена;
миграция оставляет NULL, ничего не подставляет из текущей конфигурации. Refresh и
Identity JWT guard отклоняют такую сессию; доступ к старым JWT во внешнем API
ограничен exp (+skew). Старые строки/хеши не удаляются при миграции, logout по старому
refresh и logout-all из новой сессии позволяют отозвать их. Восстановить binding
без доказательства исходной audience нельзя. Down удаляет binding; не используйте
rollback схемы как способ продления/восстановления сессий.

SessionCleanupWorker работает в API при включённых Jwt:Enabled и SessionCleanup:Enabled.
Одна транзакция выбирает ограниченное число семейств с FOR UPDATE SKIP LOCKED,
удаляет все refresh rows выбранных семейств, затем sessions. Старые audit events
и user rows сохраняются. Незавершённая транзакция откатывается целиком.

| Options/env | Default / пределы |
| --- | --- |
| `SessionCleanup__Enabled` | true; Compose: SESSION_CLEANUP_ENABLED |
| `SessionCleanup__BatchSize` | 25 семейств за итерацию; 1–100; SESSION_CLEANUP_BATCH_SIZE |
| `SessionCleanup__PollSeconds` | 300; 1–3600; SESSION_CLEANUP_POLL_SECONDS |
| `SessionCleanup__RetentionSeconds` | 86400 после absolute session expiry; 3600–604800; SESSION_CLEANUP_RETENTION_SECONDS |

Даже revoked/consumed/expired refresh сохраняются до absoluteExpiresAt сессии плюс
retention. Ранний logout не сокращает этот срок. Это сохраняет replay evidence для
всех ещё потенциально действующих семейств, включая expiry старого токена раньше нового.
На точной границе cleanup ещё сохраняет историю; удаление после неё. Несколько API
могут запускать cleanup одновременно; заблокированные сессии пропускаются до следующего цикла.

Batch ограничивает число **семейств**, а не token rows внутри одного семейства.
Удаление одного большого семейства атомарно и может занять дольше; мониторьте размер
таблицы/длительность maintenance и настройте batch/poll для своего потока.
При отключённом worker корректность rotation/replay сохраняется, растёт только история.
Очисткой audit/outbox/one-time tokens теперь занимается SecurityMaintenanceWorker этапа 6.

Локальная проверка всех уже реализованных операций (PowerShell 7.4+, Docker, Release build):

```powershell
pwsh -NoProfile -File scripts/Test-Stage6Smoke.ps1
```

Скрипт повторно использует bootstrap этапа 3, создаёт отдельный Compose project,
применяет все три миграции, проверяет rotation/restart/replay/revoke/logout-all/logout,
profile update, password change, Mailpit reset (single-use), повторный login и deactivate,
затем удаляет только свои контейнеры/network/volume/env/key files.
Только в изолированном smoke лимиты повышены для последовательности запросов;
обычный Compose сохраняет defaults 30/IP и 3/account за 60 секунд.

## Login, clients и RSA keys

SDK bootstrap по умолчанию оставляет `Jwt:Enabled=false`; login возвращает 503,
JWKS — пустой набор. Compose явно включает login и монтирует отдельный RSA private PEM.
Создание dev RSA 3072 описано в README; private key не выводится и не включается в образ.
Не перезаписывайте его при обычном запуске: restart должен сохранять действующие токены.

| Options/env | Значение/пределы |
| --- | --- |
| `Jwt__Enabled` | false; true включает login/загрузку ключей |
| `Jwt__Issuer` | Обязательный фиксированный HTTPS URI, без userinfo/query/fragment |
| `Jwt__ActiveKeyId` | 1–100 ASCII букв/цифр/`-`/`_`; production запрещает dev-/test- |
| `Jwt__PrivateKeyPath` | Абсолютный путь к private RSA PEM, ключ >=2048 (рекомендуется 3072) |
| `Jwt__PublicKeys__<kid>` | Абсолютные пути к retained/prepublished PUBLIC PEM, максимум 10, без активного kid |
| `Jwt__AccessTokenLifetimeSeconds` | 900, пределы 60–900 |
| `Jwt__ClockSkewSeconds` | 30, пределы 0–30 |
| `Identity__Clients__0__ClientId`, `Audience`, `Enabled` | Уникальный ID <=100, audience <=300, хотя бы один включённый клиент |
| `Login__FailureLimit`, `LockoutSeconds` | 5 / 900; пределы 2–20 / 60–3600 |
| `Login__RefreshLifetimeSeconds`, `SessionLifetimeSeconds` | 604800 / 2592000; пределы 60–7 дней / 60–30 дней |
| Compose `JWT_KEY_FILE`, `JWT_ISSUER` | `./secrets/jwt-dev.pem`, `https://identity.localhost` |

Пример дополнительных env при SDK-запуске (остальные настройки БД/email — ниже):

```powershell
$env:Jwt__Enabled = 'true'
$env:Jwt__Issuer = 'https://identity.localhost'
$env:Jwt__ActiveKeyId = 'dev-jwt'
$env:Jwt__PrivateKeyPath = Join-Path (Get-Location) 'secrets/jwt-dev.pem'
$env:Identity__Clients__0__ClientId = 'native'
$env:Identity__Clients__0__Audience = 'identity-native'
$env:Identity__Clients__0__Enabled = 'true'
```

Options проверяются ValidateOnStart; key ring до начала приёма запросов читает файлы,
проверяет private/public назначение и размер RSA. Ошибки не содержат PEM, путей или
внутренних исключений. Public-key files не должны содержать private components.
Файлы/настройки неизменны в течение жизни процесса, изменение требует restart.
Все replicas должны получать одни и те же ключи и client configuration.
Не переиспользуйте clientId с другой audience: для новой audience создайте новый ID.
Audience сохраняется в sessions и не меняется при refresh. Изменённая audience
в конфигурации отключает refresh существующих семейств; необходим новый login.

Ротация RS256:

1. Создайте новый RSA key вне приложения; храните private PEM в secret provider/mount
   с минимальными правами. Новый kid обязателен. Для экспорта public PEM используйте
   стандартный RSA.ExportSubjectPublicKeyInfoPem/OpenSSL; не копируйте private PEM потребителям.
2. Добавьте новый PUBLIC PEM в Jwt:PublicKeys на всех replicas, оставив старый активным.
   Перезапустите их и обновите JWKS-кэши потребителей (endpoint cache — 300 секунд).
3. Переключите ActiveKeyId/PrivateKeyPath, а старый PUBLIC PEM оставьте в PublicKeys.
   Не оставляйте новый активный kid одновременно в PublicKeys. Выполните restart.
4. После последнего JWT старого ключа выждите его lifetime + clock skew и период
   обновления кэшей потребителей, затем удалите старый public key из JWKS.

Не используйте RSA key как AES outbox key; backups/доступ к этим секретам раздельные.
JWT revocation в сторонних API ограничен exp. Emergency key compromise требует
отдельного оперативного обновления JWKS-кэшей всех потребителей.

Локальный bootstrap описан в README. Dockerfile собирает API в .NET 10 SDK image,
публикует Release и запускает результат в ASP.NET runtime image от non-root пользователя.
Контекст сборки ограничен исходниками и общими файлами сборки; `.env` не копируется.

Compose поднимает API, PostgreSQL 17 и Mailpit. PostgreSQL проверяется `pg_isready`
до запуска API. API использует БД и SMTP через transactional outbox.
Успешный readiness не означает готовность регистрации/входа или актуальность схемы.

| Переменная | Назначение | Значение/источник |
| --- | --- | --- |
| `POSTGRES_PASSWORD` | Локальный пароль БД | Обязателен в `.env`, пример пустой |
| `Database__ConnectionString` | EF/Npgsql connection string | Обязательна для SDK/EF; Compose формирует из POSTGRES_PASSWORD |
| `OUTBOX_KEY_FILE` | Локальный файл для Compose secret | `./secrets/outbox-dev.key`, 32 random raw bytes |
| `EMAIL_VERIFICATION_URL_TEMPLATE` | Фиксированный server-side template | `.env.example`, один `{token}`, рекомендуемый fragment |
| `RESET_PASSWORD_URL_TEMPLATE` | Фиксированный reset template, обязателен для Compose | `.env.example`, один `{token}`, рекомендуемый fragment |
| `AUTH_IP_PERMIT_LIMIT`, `AUTH_ACCOUNT_PERMIT_LIMIT` | Локальные Compose rate limits | 30 / 3 за 60 секунд |
| `API_PORT` | Порт API на хосте | `5080` |
| `POSTGRES_PORT` | Порт БД на хосте | `54320` |
| `MAILPIT_HTTP_PORT` | Mailpit UI | `8025` |
| `MAILPIT_SMTP_PORT` | Mailpit SMTP | `1025` |
| `ASPNETCORE_ENVIRONMENT` | Среда API | `Development` в Compose/launch profile, иначе `Production` |
| `ASPNETCORE_HTTP_PORTS` | Порт внутри контейнера | `8080` |
| `AllowedHosts` | Допустимые HTTP Host headers | `localhost;127.0.0.1;[::1]`; для иного DNS имени задать явно |

Операционные значения задаются стандартной конфигурацией ASP.NET Core, включая env.
DatabaseOptions валидируются на старте: нужны Host, Database, Username; error detail
и parameter logging должны быть выключены. Ошибка конфигурации не содержит её значения.
Строку подключения задавайте через защищённую среду/secret provider, не в appsettings
или Git. `.env` не импортируется автоматически в окружение SDK-процесса.

## Миграции

Локальный dotnet-ef зафиксирован в `.config/dotnet-tools.json`. Design-time factory
находится в Infrastructure и использует только `Database__ConnectionString`.
Для новой/существующей БД применяйте миграции отдельной командой с отдельными
deployment credentials. API не вызывает Migrate/EnsureCreated на старте.

```powershell
dotnet tool restore
$env:Database__ConnectionString = Read-Host 'PostgreSQL connection string' -MaskInput
dotnet ef migrations list --project src/Identity.Infrastructure --configuration Release
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release
dotnet ef database update --project src/Identity.Infrastructure --configuration Release
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --output migration.sql
```

Начальная миграция `InitialIdentity` создаёт все семь таблиц раздела 5, FK, индексы,
check constraints и concurrency mapping xmin. Проверки Up/Down запускаются только
на временной БД Testcontainers. Не применяйте `database update 0` к рабочим данным:
это удаляет таблицы. Перед будущими schema changes нужны backup и проверка SQL.
API runtime в production должен иметь только необходимые DML-права, миграционный
процесс — DDL-права. Сначала миграции, затем запуск/обновление API.

Readiness `/health/ready` проверяет соединение, отвечает 200/503 без деталей и должен
быть закрыт на сетевом уровне. Не проксируйте его в публичную сеть. Локально порты
Compose уже привязаны к loopback; `/health/live` остаётся проверкой только процесса.

Dockerfile и dev Compose закреплены на проверенные digests .NET 10/PostgreSQL 17/Mailpit.
Обновляйте их осознанно вместе с full tests/smoke; pinned image не получает patches
без rebuild. Отдельная [production-инструкция](production.md) описывает immutable release
image, TLS/reverse proxy, secret mounts/keys, SMTP TLS, backups и migrations.
Dev Compose с Mailpit и Development не используется как production deployment.

Container HEALTHCHECK запускает `dotnet Identity.Api.dll --health-check`, exit 0
только при 200 healthy от loopback /health/live. ASPNETCORE_HTTP_PORTS должен содержать
один числовой port (в образе 8080), IDENTITY_HEALTH_HOST — разрешённый Host (default
localhost). Redirect/proxy для probe выключены, timeout 3 секунды; shell/curl/SDK
в runtime не нужны. Docker health сообщает liveness, readiness/schema/SMTP проверяются
отдельно. Compose --wait теперь ожидает этот healthcheck, а не только running state.

## Регистрация и письма

В appsettings регистрация, password reset и worker выключены: existing health/EF bootstrap не требует
SMTP/key. Dev Compose включает их, указывает Mailpit без TLS и монтирует отдельный
raw 32-byte AES key в `/run/secrets/identity_outbox_key`. Генерация ключа описана в README;
автоматической генерации при старте API нет. Не перезаписывайте ключ, пока есть backlog.

Пример SDK-конфигурации в PowerShell после подготовки `.env`/ключа и запуска Mailpit:

```powershell
$env:Registration__Enabled = 'true'
$env:PasswordReset__Enabled = 'true'
$env:Outbox__Enabled = 'true'
$env:OutboxEncryption__ActiveKeyId = 'dev-outbox'
Set-Item Env:OutboxEncryption__Keys__dev-outbox (Join-Path (Get-Location) 'secrets/outbox-dev.key')
$env:Email__FromAddress = 'no-reply@identity.example'
$env:Email__VerificationUrlTemplate = 'http://localhost:3000/verify#token={token}'
$env:Email__ResetPasswordUrlTemplate = 'http://localhost:3000/reset#token={token}'
$env:Email__Smtp__Host = '127.0.0.1'
$env:Email__Smtp__Port = '1025'
$env:Email__Smtp__Tls = 'None'
dotnet run --project src/Identity.Api --launch-profile Identity.Api
```

`Database__ConnectionString` остаётся обязательной. Compose `.env` автоматически не
экспортируется в SDK-процесс. Браузерная страница из template принадлежит владельцу
инсталляции и не поставляется этим backend; локально токен копируется из Mailpit в POST verify.

| Options/env section | Настройки |
| --- | --- |
| `Registration__Enabled` | false по умолчанию; публичная регистрация/resend |
| `PasswordReset__Enabled` | false по умолчанию; forgot/reset; Compose включает true |
| `PasswordHash__IterationCount` | 600000, диапазон 600000–2000000 |
| `Email__Smtp__Host`, `Port`, `Tls` | Tls: None (dev), StartTls или SslOnConnect |
| `Email__Smtp__UserName`, `Password` | Только env/secret provider; оба заполнены либо оба пусты для trusted relay |
| `Email__Smtp__TimeoutSeconds` | 10, диапазон 1–60 секунд |
| `Email__FromAddress` | Обязателен при включении registration/reset/worker |
| `Email__VerificationUrlTemplate`, `Email__ResetPasswordUrlTemplate` | Обязателен template включённого producer; ровно один `{token}`, HTTPS в production, dev HTTP только loopback, без userinfo |
| `OutboxEncryption__ActiveKeyId`, `OutboxEncryption__Keys__<id>` | ID активного ключа и абсолютные пути к raw key files; до 10 retained keys |
| `Outbox__Enabled`, `BatchSize`, `PollSeconds` | false / 10 / 5; batch 1–100, poll 0.05–60 секунд |
| `Outbox__MaxAttempts`, `BaseRetrySeconds` | 8 / 10; attempts 1–20, retry base 0.1–300 секунд, exponential cap 6 часов |
| `AuthRateLimits__IpPermitLimit`, `AccountPermitLimit`, `WindowSeconds` | 30 / 3 / 60; локальные для экземпляра |

При startup проверяются options и доступность/размер key files. Production с включённой
регистрацией или password reset требует worker, HTTPS URL и TLS SMTP. ID с префиксами `dev-`/
`test-` не допускаются для используемого production key ring. Пустые/all-zero ключи
отклоняются; оператор обязан предоставить криптографически случайные production keys.
SMTP сертификаты проверяются стандартным MailKit/.NET способом, permissive callbacks нет.
Для trusted relay без user/password оператор обеспечивает сетевой контроль доступа.

Ротация AES key: добавьте новый key ID/file, переключите ActiveKeyId и перезапустите
процесс, сохраняя старые key files до отправки/разбора всех pending и failed messages.
После успешной отправки payload удаляется сразу. Если письмо принято SMTP, но DB commit
не произошёл, оно может прийти повторно. Шаблоны/потребители должны учитывать это.

Worker держит одну DB-транзакцию на письмо и claim с FOR UPDATE SKIP LOCKED. При
ошибке сохраняет только постоянный код и backoff, не сообщение исключения. AttemptCount
считает неуспешные попытки. После MaxAttempts письмо не удаляется и больше не выбирается;
сигнал — error log с message ID и metric. Оператор устраняет SMTP/key проблему и может
адресно сбросить attempt_count/next_attempt_at для нужной записи через защищённый DB tooling.
Не удаляйте encryption keys раньше backlog, включая failed messages.

Meter `Identity.Email`: `identity.email.outbox.pending`, `identity.email.outbox.failed`,
`identity.email.delivered`, `identity.email.delivery_failures`. Gauges обновляются в
worker loop, labels не содержат PII. Экспортёр/операторское оповещение настраиваются отдельно.
В dev Mailpit отключены reverse DNS lookups, чтобы изолировать SMTP от внешнего DNS:
[Mailpit runtime options](https://mailpit.axllent.org/docs/configuration/runtime-options/).
