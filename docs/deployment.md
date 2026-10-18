# Запуск и конфигурация Identity — этап 0

Локальный bootstrap описан в README. Dockerfile собирает API в .NET 10 SDK image,
публикует Release и запускает результат в ASP.NET runtime image от non-root пользователя.
Контекст сборки ограничен исходниками и общими файлами сборки; `.env` не копируется.

Compose поднимает API, PostgreSQL 17 и Mailpit. PostgreSQL проверяется `pg_isready`
до запуска API. API не использует БД/SMTP до следующих этапов; отсутствие readiness
проверки нельзя интерпретировать как готовность к регистрации/входу.

| Переменная | Назначение | Значение/источник |
| --- | --- | --- |
| `POSTGRES_PASSWORD` | Локальный пароль БД | Обязателен в `.env`, пример пустой |
| `API_PORT` | Порт API на хосте | `5080` |
| `POSTGRES_PORT` | Порт БД на хосте | `54320` |
| `MAILPIT_HTTP_PORT` | Mailpit UI | `8025` |
| `MAILPIT_SMTP_PORT` | Mailpit SMTP | `1025` |
| `ASPNETCORE_ENVIRONMENT` | Среда API | `Development` в Compose/launch profile, иначе `Production` |
| `ASPNETCORE_HTTP_PORTS` | Порт внутри контейнера | `8080` |
| `AllowedHosts` | Допустимые HTTP Host headers | `localhost;127.0.0.1;[::1]`; для иного DNS имени задать явно |

Операционные значения задаются стандартной конфигурацией ASP.NET Core, включая env.
Прикладных options с секретами пока нет: JWT, SMTP и outbox не реализованы.
Схемы PostgreSQL и миграций тоже нет. Никаких `EnsureCreated` или автоматических
миграций на старте API нет; deployment-команды миграций добавляются с этапом 1.

Образы используют stable tags `10.0`, `17-alpine`, `v1`, чтобы получать обновления
соответствующих линеек. Перед будущей production-поставкой нужно закрепить проверенные
digests и настроить их обновление. Полная production-инструкция относится к этапу 7.
В ней потребуются TLS/reverse proxy, secret mounts и постоянные JWT/outbox ключи,
SMTP с TLS, backups PostgreSQL, отдельный запуск миграций и retention policies.
Текущий Compose с Mailpit и Development не является production deployment.
