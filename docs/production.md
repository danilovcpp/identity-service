# Identity 1.0.0: production deployment

**Identity1.1:** актуальная инструкция — [Console production/runbook](console-production.md).
Ниже сохранён backend/API-only runbook1.0 для описания grants, keys и recovery.
Его topology/Compose команды заменены этапом12: API больше не публикует host-порт,
добавлен TLS web edge, обязательные CIDR/config/TLS/Data Protection mounts и все6
миграций. Используйте новый guide для подготовки и запуска текущего Compose.

Этап10 [admin writes/invitations](admin-write-api.md): примените AdminWriteLifecycle,
настройте отдельный Invitations opt-in и fragment-only activation/reset URLs. Production
invitations требуют outbox worker + SMTP TLS/AES keys; UI pages будут в этапе11. Все
admin endpoints по-прежнему за VPN/доверенным контуром до MFA.

Для дополнения v1.1 (этап 8) дополнительно выполните [admin access runbook](admin-access.md):
отдельный постоянный writable Data Protection mount, CLI bootstrap, HTTPS и VPN/сетевое
закрытие всего admin API. Текущий Compose не включает admin автоматически. После restore
отзовите также `admin_sessions` и проверьте восстановленные роли до открытия трафика.

Инструкция для Linux/Docker с PostgreSQL 17+, SMTP с TLS и отдельным TLS terminator.
Она не выполняет внешних действий автоматически. `compose.yaml` с Mailpit предназначен
для локальной разработки; production использует отдельный `deploy/compose.production.yaml`.
API остаётся тем же бинарным артефактом, настройки задаются без изменения C#.

## 1. Артефакт и секреты

Собирайте из reviewed clean commit. На доверенном build runner выполните CI-команды README, затем:

```sh
docker build --build-arg VCS_REF="$(git rev-parse HEAD)" --tag identity:1.0.0 .
docker image inspect identity:1.0.0 --format '{{.Id}} {{.Config.User}}'
```

Публикация в ваш registry — отдельное операторское действие. После публикации зафиксируйте
его digest в IDENTITY_IMAGE. Не используйте floating tag как production release identity.
В Dockerfile закреплены официальные MCR SDK/runtime manifests; dev PostgreSQL/Mailpit
тоже pinned. При обновлении OS/.NET/images проверяйте новый digest, rebuild, NuGet audit,
тесты и smoke. Не считайте закреплённый digest автоматически актуальным навсегда.

Подготовьте отдельные постоянные RSA PKCS8 PEM (рекомендуется 3072 бит) и AES raw
32 random bytes через secret tooling. Генерация стандартными API показана в README;
для production задавайте новые IDs без dev-/test- и отдельные файлы. API их не генерирует.
Compose secrets — read-only file mounts, не encrypted secret vault: защитите host files
и backups. Non-root UID образа должен иметь read access к каждому key file (проверьте
Config.User); остальные пользователи — нет. Не делайте ключи world-readable ради запуска.
Private RSA не передаётся consumers, AES хранится отдельно от PostgreSQL/backups.

Скопируйте `deploy/production.env.example` в корневой `.env.production` через защищённое
tooling, ограничьте права файла владельцем и замените все `<...>` placeholders.
Не коммитьте и не печатайте итоговый env/config; `docker compose config` может раскрыть
значения, поэтому используйте `config --quiet`. Env подходит для закрытого single-host
deployment; оператор с Docker/root доступом видит env. Для строгой изоляции используйте
доверенный secret provider, сохраняя те же options/environment inputs.

## 2. PostgreSQL и отдельные миграции

Выделите постоянный PostgreSQL volume/storage, backup/PITR и TLS. По сети доступен только
API и migration runner; public 5432 не нужен. Пример использует SSL Mode=VerifyFull;
серверный сертификат должен быть доверенным и соответствовать hostname. Для собственной
CA смонтируйте CA certificate read-only и добавьте Root Certificate в connection string.
Include Error Detail/Log Parameters и EF sensitive logging запрещены.

Создайте отдельные роли `identity_migrator` (владелец схемы) и `identity_app` (runtime),
пароли — через secret tooling/интерактивный psql `\password`, не SQL literals в истории.
В новой выделенной БД migrator владеет public/schema objects. API не требует superuser,
CREATEDB, CREATEROLE или DDL. После миграций выдайте runtime:

```sql
GRANT CONNECT ON DATABASE identity TO identity_app;
GRANT USAGE ON SCHEMA public TO identity_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE users, credentials, sessions,
  refresh_tokens, one_time_tokens, outbox_messages, security_audit_events TO identity_app;
GRANT SELECT ON TABLE identity_administrators TO identity_app;
GRANT SELECT, INSERT, UPDATE ON TABLE admin_sessions TO identity_app;
```

На доверенном runner с SDK и исходниками этого выпуска задайте migration
Database__ConnectionString через protected environment, затем:

```sh
dotnet tool restore
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
dotnet ef database update --project src/Identity.Infrastructure --configuration Release --no-build
```

Review migration.sql перед применением; вместо последней EF команды DBA может применить
его через `psql --set ON_ERROR_STOP=1 --file migration.sql` с отдельным защищённым
PGSERVICEFILE/PGPASSFILE. Выполняйте **один** выбранный путь. Скрипт включает три
миграции выпуска; API auto-migrate не делает. Удалите migration credentials из окружения
runner после применения. Runtime env содержит только identity_app connection string.

## 3. TLS terminator, routing и SMTP

Выберите реальный DNS/issuer (например, identity.example), настройте сертификат и
TLS на reverse proxy. Public HTTP отклоняйте на edge до чтения credentials; backend
дополнительно возвращает 400 https_required. Proxy отправляет backend HTTP через
loopback/private network, заменяет X-Forwarded-For и X-Forwarded-Proto на проверенные
значения, сохраняет Host identity.example. Не пропускайте произвольные client headers.

В `.env.production` задайте:

- AllowedHosts: реальный DNS и localhost для внутреннего healthcheck.
- TransportSecurity:ForwardedHeadersEnabled=true, KnownProxies:0 — **точный IP**,
  видимый API у вашего proxy; ForwardLimit=1 для одного terminator. Не используйте
  автоматический ASPNETCORE_FORWARDEDHEADERS_ENABLED/trust-all.
- В host-proxy → Docker published-port topology API обычно видит gateway bridge,
  а не 127.0.0.1: проверьте `docker network inspect identity-production_default` и
  фактический маршрут до открытия трафика. Не угадывайте адрес по примеру.
- Закройте `/health/ready` и `/health/live` на публичном proxy. Docker probe обращается
  только к 127.0.0.1 внутри контейнера; при ином AllowedHosts задайте IDENTITY_HEALTH_HOST.
- Public routes: `/api/v1/*` и `/.well-known/jwks.json`. OpenAPI в production выключен.
- Не логируйте body, Authorization, raw URL/query, SMTP protocol или secrets на proxy/APM.
  Согласуйте aggregate IP/account limits для нескольких replicas; CORS API выключен.

SMTP: действительный host/port, StartTls или SslOnConnect, корректная цепочка сертификата;
парные UserName/Password из secret provider либо оба пусты для сетево защищённого relay.
Укажите FromAddress и фиксированные HTTPS verification/reset templates с одним {token}.
Страницы app.example — внешняя UI/BFF оператора, backend их не поставляет. Registration,
PasswordReset и Outbox включены в sample; неверная production конфигурация не стартует.

## 4. Запуск и проверка

Из корня deployment checkout, после миграций и подготовки env/keys:

```sh
docker compose --env-file .env.production -f deploy/compose.production.yaml config --quiet
docker compose --env-file .env.production -f deploy/compose.production.yaml up --detach --wait
docker compose --env-file .env.production -f deploy/compose.production.yaml ps
docker compose --env-file .env.production -f deploy/compose.production.yaml exec -T api dotnet Identity.Api.dll --health-check
```

Шаблон запускает только API: read-only rootfs, bounded /tmp, dropped capabilities,
no-new-privileges, memory/pids/log limits. Нужен image с HEALTHCHECK из этого выпуска.
Внутри контейнера один HTTP port 8080; health CLI timeout 3 секунды, без redirect/proxy,
exit 0 только для `200 {status:healthy}`. Docker health — liveness, не доказательство
актуальности schema/SMTP; restart policy сама по себе не перезапускает unhealthy процесс.
Настройте внутреннюю readiness проверку, alerting и процедуру перезапуска отдельно.

С внутреннего probe через proxy HTTPS проверьте health/ready и JWKS; отдельной тестовой
учётной записью пройдите register → реальное письмо → verify → login → refresh → me →
logout → failed refresh. Не публикуйте полученные tokens. Проверьте unknown account
generic responses, HTTPS/HSTS и отсутствие секретов в собранных логах.
Локальный эквивалент с временными ресурсами: `pwsh -File scripts/Test-Stage7Smoke.ps1`.

## 5. Обновления, ротация и восстановление

До обновления сохраните encrypted PostgreSQL backup, env/config и отдельные защищённые
RSA/AES backups. Test restore обязателен в изолированной БД. Остановите/drain API при
несовместимом schema change, выполните reviewed migrations, проверьте runtime grants,
затем замените immutable image digest и выполните readiness/smoke. Для текущего
выпуска схема — три существующие миграции, новых в этапе 7 нет. Schema rollback
не заменяет restore; `database update 0` удаляет таблицы и к production не применяется.

RSA rotation с retained public keys и AES rotation до разбора encrypted backlog
описаны в [deployment](deployment.md#login-clients-и-rsa-keys) и разделе email.
На всех replicas keys/client config должны совпадать; смена audience существующего
clientId требует нового login. Сохраняйте старые AES keys до доставки/разбора pending
и failed messages, включая восстановленные из backup. Секреты не лежат в Docker image.

Для PostgreSQL backup используйте `pg_dump --format=custom --file=identity.backup` с
защищёнными PGSERVICEFILE/PGPASSFILE либо согласованное PITR. Шифруйте и ограничивайте
доступ к backup. Проверочный `pg_restore --exit-on-error --dbname=identity_restore`
выполняйте только в специально созданную изолированную БД, затем проверяйте migrations,
constraints и данные. Retention backup и удаление PII — отдельная операторская политика.

Restore старого snapshot откатывает также password/status/revocation и может воскресить
старые credentials/tokens. Перед возвратом трафика выполните security reconciliation
с журналом инцидентов/изменений и аннулируйте все восстановленные sessions/reset links.
Пример для **восстановленной, закрытой от трафика** БД (1 = LogoutAll):

```sql
BEGIN;
UPDATE sessions SET revoked_at = GREATEST(CURRENT_TIMESTAMP, created_at), revoke_reason = 1 WHERE revoked_at IS NULL;
UPDATE refresh_tokens SET revoked_at = GREATEST(CURRENT_TIMESTAMP, created_at) WHERE revoked_at IS NULL;
UPDATE admin_sessions SET revoked_at = GREATEST(CURRENT_TIMESTAMP, created_at) WHERE revoked_at IS NULL;
UPDATE one_time_tokens SET consumed_at = created_at WHERE consumed_at IS NULL;
COMMIT;
```

Это не восстанавливает события, потерянные после snapshot: старый password hash и
статус могут требовать контролируемого password recovery/ручного разбора до открытия
доступа. При key compromise проведите emergency rotation у всех consumers; обычный
logout не отзывает JWT в downstream до exp (+skew).

Мониторьте [meters/retention](deployment.md#этап-6-транспорт-retention-и-наблюдаемость):
login failures/replay, HTTP errors/latency, stale sampled_at, outbox pending/failed,
maintenance errors, DB storage/connections, key/cert expiry. Экспортёр/collector —
операторская интеграция без public unauthenticated metrics endpoint.
Ограничения v1 собраны в [CHANGELOG](../CHANGELOG.md#ограничения) и [security](security.md).
