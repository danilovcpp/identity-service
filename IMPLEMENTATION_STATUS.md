# Отчёт по этапу 5

## 1. Этап

**5 — Profile и password lifecycle**, завершён 2026-10-19.
Источник требований: Identity_Codex_Spec.md, §6.3–6.5, §7, §10 (этап 5), §11–13.
Перед изменениями прочитаны AGENTS.md и спецификация, проверены код и история Git.
Этапы 0–4 подтверждены commits d7c4f94, 8d901d7, 647ae4c, 4f64b57, 6e793da
и существующими тестами. Реализован только этап 5; этапы 6–7 не начинались.

## 2. Реализовано

- GET/PATCH `/api/v1/users/me`: собственный профиль из проверенных sub/sid,
  явная безопасная projection, no-store. Обновляется только displayName;
  explicit null очищает его, неизвестные поля/email и отсутствие поля дают 400.
- POST `/api/v1/auth/password/change`: текущий пароль, политика нового,
  новый hash/PasswordChangedAt, отзыв всех сессий и refresh, аннулирование reset links,
  audit password_changed. Пустой 204 без автоматического login.
- POST `/api/v1/auth/password/forgot`: одинаковый 202; только Active получает письмо
  на сохранённый email. RNG token на 30 минут, только SHA-256 в one_time_tokens;
  полностью зашифрованное AES-GCM письмо и token сохраняются одной транзакцией.
- POST `/api/v1/auth/password/reset`: atomic consume, новая credential, сброс временной
  login-блокировки, отзыв всех сессий/refresh и прочих reset links, audit password_reset.
  Одинаковый 400 для непригодных links; повторное использование невозможно.
- POST `/api/v1/users/me/deactivate`: проверка текущего пароля, терминальная деактивация,
  отзыв всех сессий/refresh/reset links, audit account_deactivated. Дальнейший login —
  generic 401; forgot — generic 202 без письма. PII не удаляется.
- IP/account rate limiting, ограничение body, безопасные ProblemDetails и startup
  validation встроены в существующую реализацию. Worker доставляет password_reset
  прежним SMTP/retry/claim механизмом с очисткой payload после success.

Ключевые файлы (относительно корня репозитория):

- `src/Identity.Api/Controllers/UsersController.cs`, `Controllers/AuthController.cs`,
  `Contracts/PasswordRequests.cs`, `Contracts/UpdateProfileRequest.cs`,
  `Configuration/AccountRateLimitFilter.cs`, `Middleware/ApiExceptionHandler.cs`.
- `src/Identity.Application/Features/Users/` (GetCurrentUser/UpdateProfile/ChangePassword/
  DeactivateAccount), `Features/Auth/RequestPasswordReset/`, `Features/Auth/ResetPassword/`;
  отдельные commands/handlers/validators, новые узкие persistence/notification/settings
  интерфейсы, `Common/SessionRevoker.cs`, `Common/PasswordInput.cs`, явная DI.
- `src/Identity.Infrastructure/Persistence/Repositories/ProfileRepository.cs`,
  `PasswordResetRepository.cs`, `Email/PasswordResetEmailFactory.cs`,
  `Email/PasswordResetOptions.cs`, настройки email/key ring/worker и DI.
- `tests/Identity.IntegrationTests/AccountLifecycleTests.cs` (39 случаев),
  `tests/Identity.UnitTests/AccountValidationTests.cs` (15 случаев),
  `tests/Identity.ApiTests/HttpSmokeTests.cs` (+4 случая).
- `scripts/Test-Stage5Smoke.ps1`, общий bootstrap `scripts/Test-Stage3Smoke.ps1`,
  `compose.yaml`, `.env.example`, `appsettings.json`, README и четыре документа `docs/`.

## 3. Решения

Clean Architecture, MVC Controllers и собственный CQRS сохранены. Controllers не
получают DbContext/repositories. Domain, project references, версии NuGet и схема
не менялись: существующие enums, entities и две миграции покрывают этап 5.
LogoutAll использует извлечённый общий scoped SessionRevoker без изменения контракта.

Мутации сериализуются через user FOR UPDATE, затем session/token locks; транзакция
включает credential/status, token consume, bulk revoke/invalidation и audit. Reset
до lock читает только owner ID, а tracked token загружает заново после user lock:
ожидающий конкурентный запрос видит уже committed consumption. Текущий tracked token
исключён из bulk invalidation, чтобы сохранить xmin до SaveChanges. Аннулируются
также links с опережающим createdAt; consumedAt ограничен снизу createdAt для SQL constraints.

Согласно §6.3 отзываются **все**, включая текущую, сессии без выдачи новой пары.
Дополнительная политика links: новый forgot аннулирует старые; change/reset/deactivate
аннулируют все оставшиеся неистёкшие reset links. Failed validation/confirmation и
сбой до commit сохраняют прежнее состояние целиком. Recovery снимает только временную
login-блокировку, не Disabled/Deactivated. Email в v1 неизменяемый (§6.5).

PasswordReset:Enabled независим от Registration и по умолчанию false для SDK bootstrap;
Compose явно включает его. Email/SMTP/AES проверяются при любом включённом producer
или worker; template обязателен своему producer. Production reset требует worker,
HTTPS template, SMTP TLS и key ring без dev-/test- IDs. Ошибки не раскрывают конфигурацию.
Unknown/ineligible forgot выполняет RNG/encryption без записи, сохраняя общий ответ;
гарантии постоянного времени нет. Противоречий требованиям этапа 5 не выявлено.

## 4. Проверки

Выполнены из корня репозитория (.NET SDK 10.0.402, Docker Linux containers):

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger "trx;LogFilePrefix=stage5-final"
dotnet tool restore
# Для design-time проверок, без подключения к рабочей БД:
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
dotnet list Identity.slnx package --vulnerable --include-transitive
pwsh -NoProfile -File scripts/Test-Stage5Smoke.ps1
git diff --check
```

- Restore/build/format успешны, **0 warnings / 0 errors**.
- Полный финальный прогон: **295 passed, 0 failed, 0 skipped**: Unit 97, API 22,
  Integration 176. Это +58 случаев к 237 тестам этапа 4. TRX находятся в test projects'
  локальных TestResults (префикс stage5-final); не включаются в Git.
- Integration используют PostgreSQL 17/Mailpit/Testcontainers, без InMemory/SQLite.
  Проверены собственный профиль/IDOR/immutable email, неверный текущий/новый пароль,
  expiry/wrong purpose/single-use reset, generic forgot, аннулирование старых links,
  unlock временного lockout, terminal deactivation и неизменность чужих аккаунтов.
- Проверены конкурентные reset одного/разных tokens, одновременные password changes,
  refresh против change/reset/deactivate, rollback до commit, потеря ответа после
  reset commit, restart/key persistence/delivery, отсутствие секретов в логах,
  startup validation и account limiting. Предыдущие registration/rotation/replay tests проходят.
- EF: pending model changes отсутствуют, idempotent SQL успешно сформирован.
  Up/Down и ограничения схемы проверены существующей PostgreSQL suite;
  smoke применил InitialIdentity и SessionAudienceBinding к новой временной БД.
- NuGet audit: у всех семи проектов уязвимые packages не обнаружены по текущим sources.
- Docker smoke успешен, exit 0: build Release image, non-root, healthchecks,
  register → Mailpit → verify → login/JWKS → rotation/restart/replay → sessions/
  revoke/logout-all/logout; затем profile/update → change/all-session revoke →
  Mailpit forgot/reset → replay reset rejected → new login → deactivate → failed login.
  Проверены SQL state/audit, временные project containers/network/volume/env/keys удалены.
  Локальный лог: `TestResults/stage5-docker-smoke.log` (ignored).
- `git diff --check` успешен; новые файлы reviewed, private keys/.env/build artifacts
  исключены из Git. Git сообщает только ожидаемую нормализацию LF→CRLF.

Первый повторный restore/build в sandbox завершился code 1 без диагностики;
тот же набор вне sandbox успешно выполнен. Результат прерванного до продолжения
smoke не учитывался: повторный запуск с сохранённым логом подтвердил успех.

## 5. Безопасность

Закрыты IDOR профиля, изменение immutable email, повторное/конкурентное использование
reset, восстановление Disabled/Deactivated, сохранение действующих сессий после смены
пароля/деактивации, partial commit и использование старых reset links после смены пароля.
Время инъецируется через TimeProvider, все async операции получают CancellationToken.
Audit сохраняет только whitelisted metadata и correlation ID; raw пароли/токены не
пишутся в БД/логи. Outbox шифрует весь EmailMessage независимым AES key ring.

Остаются границы текущего продукта: JWT downstream действителен до exp (+skew),
локальные rate limits не суммируются между replicas и сбрасываются при restart,
SMTP at-least-once, отсутствие гарантии constant-time responses. Полная security/
observability матрица и HTTPS/proxy hardening относятся к этапу 6.

## 6. Ограничения/блокеры

Блокеров этапа 5 нет. Все применимые build/test/EF/Docker проверки выполнены.
Production SMTP/TLS инфраструктура и нагрузочный production-прогон не выполнялись;
production startup rejection проверен тестами. Compose предназначен для dev.

При обновлении существующего `.env` обязательно добавить RESET_PASSWORD_URL_TEMPLATE;
для SDK включать PasswordReset__Enabled явно и задавать Email__ResetPasswordUrlTemplate.
Внешняя reset/verify страница или BFF не входят в backend.

Деактивация сохраняет email, credential и audit; автоматической политики уничтожения
PII/восстановления нет. Эти данные остаются до отдельно утверждённой оператором
политики удаления, с учётом backups; endpoint не обещает стирание. Retention audit,
outbox и one-time tokens не расширялся; существующий cleanup refresh history сохранён.
REST protocol не является OAuth2/OIDC и не предоставляет стандартный SSO/MFA.
Production deployment, CI/release и внешние аккаунты не изменялись; commit не создавался.

## 7. Следующий этап

Рекомендация: **6 — Security hardening и observability** по ТЗ.
Выполнение только по отдельному запросу пользователя.
