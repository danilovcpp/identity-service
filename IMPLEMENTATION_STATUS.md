# Отчёт по этапу 2

Дата: 2026-10-19. Источник требований: AGENTS.md и Identity_Codex_Spec.md, прежде
всего разделы 4–9, этап 2 раздела 10 и формат раздела 13.

## 1. Этап

**2 — Register + email verification. Завершён.**

В начале рабочее дерево было чистым. Этапы 0 и 1 подтверждены кодом и коммитами
`d7c4f94` и `8d901d7`. Использована существующая архитектура, без переписывания Domain,
DbContext, миграции или CQRS dispatchers. Этап 3 не начат.

## 2. Реализовано

- `src/Identity.Application/Features/Auth/Register/`: команда, validator, handler;
  создание PendingVerification, credential, verification token hash, encrypted
  outbox и register audit event в одной явной транзакции.
- `Features/Auth/VerifyEmail/` и `RequestEmailVerification/`: отдельные команды,
  validators и handlers; single-use/24-hour expiry, атомарные consume/activation/audit,
  generic resend для неизвестного/неподходящего состояния пользователя.
- `Application/Common/PasswordPolicy.cs`, `PasswordText.cs`: NFKC, 15–128 Unicode
  code points, ограничения UTF-8, локальный common-password denylist, без Trim пароля.
  Добавлены безопасные прикладные ошибки и OperationContext для audit correlation.
- `Application/Abstractions/Notifications/`, `IEmailVerificationRepository`,
  `IPasswordPolicy`, `IRegistrationSettings`; явная регистрация handlers/validators
  в AddApplication. В Application нет ASP.NET/EF/configuration зависимостей.
- `src/Identity.Infrastructure/Security/`: PasswordService на Microsoft PasswordHasher,
  PBKDF2 с 600000 iterations и rehash-needed result; TokenGenerator на RNG/SHA-256;
  OutboxPayloadProtector на AesGcm; отдельный key ring из secret files с retained key IDs.
- `Infrastructure/Email/`: neutral Identity template, SMTP sender на MailKit,
  scoped outbox processor, BackgroundService worker, ограниченный exponential backoff,
  dead-letter signal и метрики backlog/deliveries/failures без PII labels.
- `Infrastructure/Persistence/Repositories/EmailVerificationRepository.cs`,
  изменения UserRepository/IUserRepository и UnitOfWork: явные user row locks,
  нормализация только конкретного unique-email нарушения в DuplicateEmailException.
- `src/Identity.Api/Controllers/AuthController.cs`, DTO в Contracts/RegisterRequest.cs:
  POST `/api/v1/auth/register`, `/email/verify`, `/email/verification-request`.
  Все используют CQRS; нет DbContext в контроллерах или Minimal API business endpoints.
- `Api/Configuration/`: начальные fixed-window IP/account rate limits с Retry-After;
  account keys — SHA-256 нормализованного email. Program/ApiExceptionHandler и новый
  RequestSizeLimitMiddleware обеспечивают 400/413/429/503 ProblemDetails и traceId.
- `appsettings.json`, `compose.yaml`, `.env.example`: feature flags, validated options,
  SMTP Mailpit, отдельный AES secret mount, template и безопасные defaults.
- `RegistrationSecurityTests.cs`, `RegistrationFixture.cs`, `RegistrationTests.cs`,
  расширенный HttpSmokeTests.cs: crypto/password и настоящие PostgreSQL/Mailpit scenarios.
- README и `docs/{architecture,api,security,deployment}.md` обновлены, включая curl,
  bootstrap key generation, key rotation, retry recovery, metrics и ограничения.
- NuGet CPM: Microsoft.Extensions.Identity.Core/Hosting.Abstractions 10.0.13,
  MailKit 4.18.1 и explicit PKCS 10.0.13 для устранения уязвимой транзитивной версии.

## 3. Решения

- Generic 202 не раскрывает существование email; дубликат делает тот же password hash
  и encrypted message construction. Unique индекс БД разрешает гонку; проигравшая
  транзакция откатывается без второго пользователя, token/outbox/credential/audit.
- Verify/resend блокируют user row в транзакции. Это сериализует и разные verification
  tokens одного user; второй успешный verify невозможен. Concurrency failure при
  consume даёт тот же invalid_verification_token, без 409 или SQL деталей.
- Resend не отзывает старые токены (это разрешённый выбор раздела 5.5): все ограничены
  сроком 24 часа, но после первого успешного verify статус Active блокирует остальные.
- PasswordHasher используется только как PBKDF2 primitive, без UserManager/Identity EF/UI.
  Hash/verify применяют одинаковую NFKC; пробелы сохраняются. Denylist локальный и
  начальный, не заявляется полной проверкой всех утечек. Обновление хеша при login
  относится к этапу 3; в этапе 2 проверен rehash-needed contract.
- Raw verification token — 32 случайных байта в base64url; в token table только SHA-256.
  Recipient/subject/body письма целиком зашифрованы AES-256-GCM: nonce 12 bytes, tag 16,
  AAD Identity/outbox/version/key ID. Ключи не генерируются автоматически при startup,
  не хранятся в БД/Git и не совпадают с DB/SMTP credentials.
- Worker держит row lock с FOR UPDATE SKIP LOCKED на время одного ограниченного по
  timeout SMTP send. Это простая модель для малого потока: одна DB connection на send.
  SMTP acceptance до DB commit может привести к повторной доставке; stable Message-Id
  не гарантирует дедупликацию. Семантика — at-least-once, не exactly-once.
- Successful outbox очищается сразу. После MaxAttempts ciphertext сохраняется для
  операторского восстановления, автоматические попытки прекращаются. Есть error log
  с message ID и отдельный failed-backlog gauge. AttemptCount считает failures.
- Registration/worker по умолчанию выключены для совместимости health/EF bootstrap.
  При включении требуются SMTP/template/keys; Production дополнительно требует worker,
  HTTPS URL, SMTP TLS и исключает используемые key IDs с dev-/test- prefix.
- ClientId пока проверяется по формату и сохраняется в audit, не аутентифицирует клиента;
  конфигурация разрешённых клиентов/audience относится к этапу 3 по разделу 10.
- Начальные лимиты — per-process, 30 auth requests/IP и 3 register/resend requests/email
  за 60 секунд, настраиваемые. Полное timing/distributed rate-limit hardening остаётся
  этапу 6. На клиентский Forwarded-For сервис автоматически не полагается.
- Схема этапа 1 уже покрывает этап 2: новых миграций не требуется. Подтверждены отсутствие
  model drift и применение InitialIdentity на новой реальной БД.
- При HTTP smoke исправлен дефект общей обработки body limit: BadHttpRequestException
  больше не становится 500, а объявленный oversized body отклоняется до binding с
  JSON 413. Существующие controller/CQRS/error contracts сохранены.

## 4. Проверки

Среда: .NET SDK 10.0.402/runtime 10.0.13, Docker Desktop Linux Engine, PostgreSQL 17,
Mailpit. Точные основные команды из корня:

```sh
dotnet restore Identity.slnx
dotnet tool restore
dotnet format Identity.slnx --no-restore
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet list Identity.slnx package --vulnerable --include-transitive
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef database update --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
docker compose --env-file .env.smoke --project-name identity-stage2-smoke config --quiet
docker compose --env-file .env.smoke --project-name identity-stage2-smoke up --detach --wait --wait-timeout 90 postgres mailpit
docker compose --env-file .env.smoke --project-name identity-stage2-smoke up --build --detach --wait --wait-timeout 90 api
docker compose --env-file .env.smoke --project-name identity-stage2-smoke exec -T api id
docker compose --env-file .env.smoke --project-name identity-stage2-smoke down --volumes
git diff --check
```

Также выполнялись filtered/detailed test runs для SMTP diagnostics и benchmark:

```sh
dotnet test tests/Identity.IntegrationTests --configuration Release --no-restore --filter FullyQualifiedName~RegistrationEncryptsOutbox --logger 'console;verbosity=detailed'
dotnet test tests/Identity.UnitTests --configuration Release --no-build --no-restore --filter FullyQualifiedName~PasswordHasherSupportsNfkc --logger 'console;verbosity=detailed'
```

Результаты:

- Restore/tool restore: успешно. Финальная Release build: **0 ошибок, 0 предупреждений**.
- **122 passed, 0 failed, 0 skipped**: UnitTests 50, ApiTests 11, IntegrationTests 61.
- Сохранены все проверки предыдущих этапов: architecture, DI, migrations Up/Down,
  FK/indices/check constraints, UTC/DST, CRUD, transactions и optimistic concurrency.
- Новые проверки: Unicode/bytes/spaces/denylist, hash/verify/rehash, случайные tokens,
  AES round-trip/tamper/key retention; malformed/expired/single-use verification;
  real concurrent registration, same/different-token verify, rollback всех записей
  после SaveChanges и до commit, в том числе активации/consume.
- Проверены generic resend/duplicate responses, field validation, IP/account 429,
  production startup rejection и отсутствие key-path в сообщении ошибки secret mount.
- SMTP: реальные Mailpit delivery и connection failure; retry/backoff/dead-letter,
  retained encrypted payload при сбое, concurrent processors, BackgroundService send.
- Первоначальный restore выявил NU1902/NU1903 для транзитивного PKCS 10.0.0; зависимость
  обновлена до 10.0.13. Финальный NuGet audit не обнаружил известных vulnerable packages.
- SMTP tests выявили зависимость локального Mailpit от reverse DNS; отключена через
  MP_SMTP_DISABLE_RDNS, timeout приведён к рабочим 10 секундам. SMTP tests проходят.
  См. [официальные Mailpit runtime options](https://mailpit.axllent.org/docs/configuration/runtime-options/).
- Benchmark одного PBKDF2 hash/600000 iterations: **264.5 ms** на текущей машине,
  без утверждений о production SLA и без снижения iteration count.
- Format verify и git diff --check: успешно. Запрещённых библиотек/заглушек/secret
  logging/permissive SMTP certificate callbacks в новом коде не найдено.
- EF: model drift отсутствует; InitialIdentity применена к новой Compose PostgreSQL;
  idempotent SQL сгенерирован. Новых schema changes нет.
- Docker multistage restore/publish/build и Compose startup успешны, API uid=1654(app).
- HTTP smoke на `localhost:5182`: register и duplicate -> одинаковый 202; Mailpit
  `localhost:18027` -> одно письмо; полученный из письма token -> verify 204;
  повтор -> 400; unknown resend -> 202; live/ready -> 200; auth OpenAPI содержит операции.
  Пароль/token не выводились в результаты команд. Проверка БД: status=Active,
  verified timestamp задан, successful outbox payload=NULL.
- Реальный oversized HTTP request/17KB -> **413 application/problem+json** с
  request_too_large; проверено через curl и PowerShell. Начальный 500 исправлен,
  добавлены regression tests.
- Временные `.env.smoke`, AES key, SQL script, тестовые контейнеры, сеть и только
  созданный smoke volume удалены; production/external accounts не использовались.
  Все применимые проверки этапа 2 выполнены.

## 5. Безопасность

Закрыты очевидное enumeration через registration/resend ответы, гонка уникальности,
повторное/просроченное подтверждение, частичная запись регистрации/активации, потеря
письма при временном SMTP failure, параллельный claim одной outbox записи, хранение
raw one-time token в БД/outbox и случайный вывод секретов через request records/logs.
Есть начальные IP/account/body limits и validation production delivery/key options.
Токены принимаются только в JSON; redirect URL от клиента не принимается.
Секреты и ключи в Git не добавлены, dev ports привязаны к loopback, CORS не включён.

## 6. Ограничения/блокеры

Блокеров этапа 2 нет. Rate limits локальны процессу и сбрасываются при restart;
timing equivalence не гарантируется; denylist начальный. SMTP at-least-once допускает
дубли после crash/commit failure. Failed outbox требует операторского восстановления
и retained keys; метрики нужно подключить к операторскому exporter/alerting отдельно.
Внешняя UI/BFF verification page не входит в backend. Mailpit/Compose — dev.
Login/JWT/JWKS, refresh rotation/replay, sessions API и password lifecycle не реализованы;
это следующие этапы. Полные TLS/proxy/security/retention/observability проверки — позже
по плану. Изменения оставлены в рабочем дереве; commit/deploy не выполнялись.
Расхождений с объёмом этапа 2 не выявлено; выбранные политики и ограничения приведены выше.

## 7. Следующий этап

Рекомендован **этап 3 — Login, JWT и JWKS**, только по отдельному запросу. Не начат.
