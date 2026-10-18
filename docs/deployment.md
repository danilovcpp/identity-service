# Запуск и конфигурация Identity — этапы 0–1

Локальный bootstrap описан в README. Dockerfile собирает API в .NET 10 SDK image,
публикует Release и запускает результат в ASP.NET runtime image от non-root пользователя.
Контекст сборки ограничен исходниками и общими файлами сборки; `.env` не копируется.

Compose поднимает API, PostgreSQL 17 и Mailpit. PostgreSQL проверяется `pg_isready`
до запуска API. API использует БД для readiness; SMTP пока не используется.
Успешный readiness не означает готовность регистрации/входа или актуальность схемы.

| Переменная | Назначение | Значение/источник |
| --- | --- | --- |
| `POSTGRES_PASSWORD` | Локальный пароль БД | Обязателен в `.env`, пример пустой |
| `Database__ConnectionString` | EF/Npgsql connection string | Обязательна для SDK/EF; Compose формирует из POSTGRES_PASSWORD |
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
