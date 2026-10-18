# Отчёт по этапу 3

Дата: 2026-10-19. Источники: AGENTS.md, Identity_Codex_Spec.md (§4–9, этап 3 §10, формат §13).

## 1. Этап

**3 — Login, JWT и JWKS. Завершён.**

До изменений рабочее дерево было чистым. Код и Git подтверждают этапы 0–2:
`d7c4f94` — каркас, `8d901d7` — Domain/CQRS/EF, `647ae4c` — регистрация/verification.
Существующие dispatchers, Domain, DbContext, регистрация/outbox и миграции сохранены.
Этап 4 не реализовывался.

## 2. Реализовано

- `src/Identity.Application/Features/Auth/Login/`: команда, результат, validator,
  handler. Email/password/status/client validation, generic 401, временная блокировка,
  rehash, создание session + initial refresh hash + audit в одной транзакции.
- `Application/Abstractions/Security/ILoginSettings.cs`, `Persistence/ILoginRepository.cs`,
  `Common/InvalidCredentialsException.cs`, `DependencyInjection.cs`: контракты/ошибки/DI.
  `ITokenService.cs`: lifetime в результате для точного expiresIn.
- `src/Identity.Domain/Entities/Credential.cs`: отдельный Rehash без изменения
  PasswordChangedAt; фактическая смена пароля остаётся отдельной операцией.
- `Infrastructure/Persistence/Repositories/LoginRepository.cs`: session/refresh/audit
  persistence и AsNoTracking-проверка активности пользователя/сессии.
- `Infrastructure/Security/JwtOptions.cs`, `LoginSettings.cs`, `LoginDependencyInjection.cs`:
  bounded options, clients/audiences, lockout policy, dummy password verifier, DI.
- `Infrastructure/Security/JwtKeyRing.cs`, `JwtTokenService.cs`: RS256 через Microsoft
  IdentityModel, secret-file RSA private/public PEM, startup validation, retained и
  предварительно публикуемые public keys, обязательные claims и public JWK projection.
- `Api/Configuration/JwtAuthentication.cs`, `Program.cs`: JwtBearer, строгие
  kid/alg/issuer/audience/lifetime/token_use/claims, DB-проверка активности, 401/403
  ProblemDetails, глобальный MVC AuthorizeFilter. Публичные actions явно AllowAnonymous.
- `Api/Controllers/AuthController.cs`, `Contracts/LoginRequest.cs`, `Middleware/ApiExceptionHandler.cs`:
  POST /api/v1/auth/login через CQRS, IP/account limits, generic ошибки, no-store response.
- `Api/Controllers/DiscoveryController.cs`: GET /.well-known/jwks.json с публичными
  kty/use/alg/kid/n/e и cache max-age=300; OIDC discovery не добавлен.
- `Directory.Packages.props`, Infrastructure/Api csproj: JwtBearer 10.0.13 и
  Microsoft.IdentityModel.JsonWebTokens 8.22.0. Domain/Application без новых packages.
- `tests/Identity.UnitTests/LoginSecurityTests.cs`, `IntegrationTests/LoginTests.cs`,
  `RegistrationFixture.cs`, `ApiTests/HttpSmokeTests.cs`: 60 новых тестовых случаев.
  Отдельный тестовый API использует только public JWKS и свою audience, без private
  key/Identity repositories. Protected probe существует только в test assembly.
- `compose.yaml`, `.env.example`, `appsettings.json`, README и четыре docs обновлены:
  independent RSA mount, clients, dev key generation, key rotation и consumer example
  без несуществующего OIDC Authority.
- `scripts/Test-Stage3Smoke.ps1`: изолированный Docker smoke с отдельным project,
  случайными secrets/портами, миграциями и очисткой только собственных ресурсов.

## 3. Решения

- Clean Architecture, MVC Controllers и собственный CQRS сохранены. Application
  не получает IConfiguration/EF/JWT/HTTP types; криптография — в Infrastructure.
- Login держит PostgreSQL FOR UPDATE на user во время PBKDF2 и транзакции:
  счётчики, rehash и session creation одного аккаунта сериализованы. Для небольшого
  сервиса выбран простой строгий механизм; запросы аккаунта ожидают row lock.
- Default: 5 неверных паролей → lock на 900 секунд. Обращения во время lock его
  не продлевают; после expiry начинается новая серия, успех сбрасывает счётчик.
- Неизвестный пользователь выполняет PBKDF2 на случайном dummy hash; неактивный/locked
  — проверку реального hash. Статус и client не позволяют пропустить password work.
- Session — до 30 дней, initial refresh — до 7 дней и не дольше session (§6.3).
  В БД только SHA-256 refresh; токены возвращаются после commit. Rehash сохраняет
  дату фактической смены пароля. Audit failed/success — в соответствующей транзакции.
- Jwt:Enabled=false сохраняет health/EF bootstrap без RSA key: login 503 login_disabled,
  JWKS пуст. Compose явно включает JWT; это требует валидных clients/HTTPS issuer/keys.
- RSA >=2048, dev/tests 3072; access lifetime 60–900 секунд, skew 0–30 (§6.1).
  Ключи не генерируются API, изменение требует restart; production запрещает dev-/test- IDs.
  JWT и outbox key rings загружаются до запуска email worker.
- Signature-provider cache для RSA key ring отключён: глобальный кэш IdentityModel
  способен удерживать освобождённый RSA после повторного создания host в одном процессе.
  Исправление проверено restart/rotation regression tests.
- Схема БД не менялась. Привязка audience при refresh (§6.2) будет реализована
  на этапе 4 вместе с refresh. Пока clientId нельзя переиспользовать с новой audience;
  это явно указано в deployment docs. Требования этапа 3 не изменены.
- MediatR, generic repository, ASP.NET Identity user management, auth server,
  роли или внешние бизнес-концепции не добавлены.

## 4. Проверки

Точные команды, запущенные из корня репозитория:

```powershell
dotnet restore Identity.slnx
dotnet tool restore
dotnet format Identity.slnx --no-restore
dotnet build Identity.slnx --no-restore --configuration Release
dotnet test Identity.slnx --no-build --no-restore --configuration Release --logger "trx;LogFilePrefix=stage3"
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet list Identity.slnx package --vulnerable --include-transitive
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
pwsh -NoProfile -File scripts/Test-Stage3Smoke.ps1
git diff --check
```

Результаты:

- Restore/tool restore успешны. Release build: **0 warnings, 0 errors**.
- **182 passed, 0 failed, 0 skipped**: Unit 74, API 12, Integration 96.
  Integration — реальные PostgreSQL 17/Mailpit, без EF InMemory/SQLite.
- Проверены generic failures, concurrent lockout/expiry/reset, rehash с сохранением
  PasswordChangedAt, rollback до commit, IP/account limits и исходные stage 0–2 tests.
- JWT: подпись/JWKS, wrong issuer/audience/RS512/unsigned/kid/signature, missing kid/iat,
  exp/nbf, token_use, client-audience mismatch, user-session mismatch, disabled/deactivated
  user, revoked/expired session, restart/rotation. В ошибках/собранных логах нет raw secrets.
- Отдельный consumer API принимает свою audience и отклоняет JWT другого включённого
  клиента, используя только public JWKS.
- Format verify/diff check прошли; NuGet audit не обнаружил известных уязвимостей.
- EF: **No changes have been made to the model since the last migration**.
  Idempotent migration.sql сгенерирован как ignored локальный артефакт. Новых миграций нет.
  InitialIdentity применяется/откатывается integration tests и применена к новой БД smoke.
- Smoke script выполнил с собственными --env-file/--project-name:
  `docker compose config --quiet`, `up --detach --wait postgres mailpit`,
  `dotnet ef database update --project src/Identity.Infrastructure --configuration Release --no-build`,
  `up --build --detach --wait api`, `restart api`, DB/non-root checks,
  `down --volumes --remove-orphans`.
- Docker restore/publish/image build/start успешны. Register 202 → Mailpit → verify 204 →
  login 200 → public JWKS 200 → restart → login 200; тот же public key после restart,
  разные refresh, две session/hash записи, outbox payload очищен, API non-root.
  Временные контейнеры/network/volume/env/key files удалены, удаление проверено.
- Измерение одного локального login с PBKDF2/signing/PostgreSQL transaction:
  **526.2 ms**; hash 600000 iterations — **270.7 ms** в одном из полных прогонов.
  Это latency sample, не production SLA или полноценный нагрузочный тест.

## 5. Безопасность

Закрыты: выдача JWT неподходящему статусу/клиенту, подмена подписи/алгоритма/kid,
неверные issuer/audience/type/lifetime, обход DB-активности на Identity endpoints,
потеря lockout updates при конкуренции, raw refresh в БД, утечка private RSA через
JWKS и секретов в ошибках/логах, частичная фиксация session/refresh/audit/rehash.
Private keys временных проверок случайные, committed secrets/production keys нет.
Объекты с password/access/refresh не используют record-generated ToString.

Остаются: refresh rotation/replay/logout/revoke/retention, password lifecycle,
дополнительные hardening/observability, production TLS/proxy и финальная поставка.
Rate limits локальны экземпляру; PostgreSQL lockout общий. Отзыв session не отзывает
JWT во внешнем API до exp (+skew). Полное постоянство timing не обещается.

## 6. Ограничения/блокеры

Блокеров этапа 3 нет; применимые build/test/migration/Docker проверки выполнены.
Полная версия ещё не завершена. Initial refresh выдаётся, но endpoint его обмена
отсутствует по границе этапа; profile/sessions API не добавлены. Нагрузочные проверки
на целевой production-машине не выполнялись. Compose — local Development.
Внешнее развёртывание, публикация и Git commit не выполнялись.

## 7. Следующий этап

**4 — Refresh rotation, Logout, Sessions**: конкурентная rotation/replay, фиксация
исходной audience семейства, отзыв/просмотр собственных сессий и cleanup с удержанием
необходимой истории. Только после отдельного запроса пользователя.
