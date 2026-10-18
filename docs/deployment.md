# Запуск и конфигурация Identity — этапы 0–2

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

Образы используют stable tags `10.0`, `17-alpine`, `v1`, чтобы получать обновления
соответствующих линеек. Перед будущей production-поставкой нужно закрепить проверенные
digests и настроить их обновление. Полная production-инструкция относится к этапу 7.
В ней потребуются TLS/reverse proxy, secret mounts и постоянные JWT/outbox ключи,
SMTP с TLS, backups PostgreSQL, отдельный запуск миграций и retention policies.
Текущий Compose с Mailpit и Development не является production deployment.

## Регистрация и письма

В appsettings регистрация и worker выключены: existing health/EF bootstrap не требует
SMTP/key. Dev Compose включает оба, указывает Mailpit без TLS и монтирует отдельный
raw 32-byte AES key в `/run/secrets/identity_outbox_key`. Генерация ключа описана в README;
автоматической генерации при старте API нет. Не перезаписывайте ключ, пока есть backlog.

Пример SDK-конфигурации в PowerShell после подготовки `.env`/ключа и запуска Mailpit:

```powershell
$env:Registration__Enabled = 'true'
$env:Outbox__Enabled = 'true'
$env:OutboxEncryption__ActiveKeyId = 'dev-outbox'
Set-Item Env:OutboxEncryption__Keys__dev-outbox (Join-Path (Get-Location) 'secrets/outbox-dev.key')
$env:Email__FromAddress = 'no-reply@identity.example'
$env:Email__VerificationUrlTemplate = 'http://localhost:3000/verify#token={token}'
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
| `PasswordHash__IterationCount` | 600000, диапазон 600000–2000000 |
| `Email__Smtp__Host`, `Port`, `Tls` | Tls: None (dev), StartTls или SslOnConnect |
| `Email__Smtp__UserName`, `Password` | Только env/secret provider; оба заполнены либо оба пусты для trusted relay |
| `Email__Smtp__TimeoutSeconds` | 10, диапазон 1–60 секунд |
| `Email__FromAddress`, `Email__VerificationUrlTemplate` | Обязательны при включении registration/worker |
| `OutboxEncryption__ActiveKeyId`, `OutboxEncryption__Keys__<id>` | ID активного ключа и абсолютные пути к raw key files; до 10 retained keys |
| `Outbox__Enabled`, `BatchSize`, `PollSeconds` | false / 10 / 5; batch 1–100, poll 0.05–60 секунд |
| `Outbox__MaxAttempts`, `BaseRetrySeconds` | 8 / 10; attempts 1–20, retry base 0.1–300 секунд, exponential cap 6 часов |
| `AuthRateLimits__IpPermitLimit`, `AccountPermitLimit`, `WindowSeconds` | 30 / 3 / 60; локальные для экземпляра |

При startup проверяются options и доступность/размер key files. Production с включённой
регистрацией требует worker, HTTPS verification URL и TLS SMTP. ID с префиксами `dev-`/
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
