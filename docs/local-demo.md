# Локальное демо Identity

Из корня репозитория, Windows / PowerShell 7.4+:

```powershell
pwsh -NoProfile -File scripts/Start-Demo.ps1
```

Нужны запущенный Docker Desktop с Linux containers, .NET SDK 10, Node.js подходящей
для frontend версии и npm (см. `src/Identity.Admin.Web/README.md`). Скрипт восстанавливает
.NET tools/packages, собирает solution и выполняет `npm ci`. Первый запуск требует
доступа к package registries / Docker images. Если HTTPS development certificate ещё
не доверен, `dotnet dev-certs https --trust` может показать системный запрос доверия.
Скрипт не отключает проверку TLS и не меняет production-конфигурацию.

Запускаются PostgreSQL 17 и Mailpit в отдельном Compose project `identity-demo`;
API и Vite работают скрытыми локальными процессами .NET / Node.js. Это локальный
SDK-сценарий, без production Docker-пакетирования frontend из этапа 12.
Compose использует существующий `compose.yaml`, отдельный env и отдельный volume.
Обычный `.env` и БД Compose project `identity` не меняются.

| Сервис | Адрес |
| --- | --- |
| Identity Console | https://localhost:5173/admin |
| HTTPS API | https://localhost:5443 |
| HTTP API / health | http://127.0.0.1:5080/health/ready |
| Mailpit | http://localhost:8025 |
| PostgreSQL | 127.0.0.1:54320 |
| SMTP Mailpit | 127.0.0.1:1025 |

Порты фиксированы. При конфликте скрипт останавливается, не завершает чужие процессы.
После успешной проверки открывается браузер. Для запуска без браузера добавьте `-NoBrowser`.

## Учётки и повторный запуск

Скрипт создаёт 12 Active + email-verified пользователей через существующие register /
Mailpit / verify API. Роли назначаются штатным CLI, прямых SQL seed нет:

- `admin@demo.identity.example`: `IdentityAdministrator`, полный доступ к консоли.
- `reader@demo.identity.example`: `IdentityReadOnly`, только чтение.
- `user01@demo.identity.example` … `user10@demo.identity.example`: обычные пользователи,
  без административных прав. Они видны в каталоге; вход в консоль им не предоставляется.

Каждый пароль генерируется криптографическим RNG. По явному запросу оператора все
логины и пароли записываются в **`secrets/demo/ACCOUNTS.md`** с пометкой:
«Это не секреты, а просто тестовые учётки для локальной отладки».
Файл предназначен для этого локального демо и исключён из Git вместе с `secrets/`.
В консоль и сервисные логи пароли автоматически не выводятся.
Явный просмотр в терминале также доступен отдельной командой:

```powershell
pwsh -NoProfile -File scripts/Start-Demo.ps1 -ShowCredentials
```

Пароли дополнительно сохраняются как PSCredential в `accounts.clixml` с Windows DPAPI:
повторный запуск требует того же Windows-пользователя на той же машине.
Папка `secrets/demo` доступна текущему пользователю Windows и SYSTEM.
Не удаляйте её отдельно от volume: она содержит настройки БД, JWT/outbox keys,
Data Protection ring и пароли. Скрипт не присоединяет новый env к старому orphaned volume.

Повторный запуск перезапускает собственные API/Vite процессы, применяет недостающие
миграции и сохраняет БД, ключи, учётки и пароли. Новые копии пользователей не создаются.
Скрипт проверяет вход всех 12 пользователей, завершает созданные проверочные native
сессии и проверяет cookie/CSRF вход, профиль и dashboard обеих административных ролей.
Изменённые вручную пароли/статусы seed-пользователей автоматически не сбрасываются:
в этом случае проверка сообщит ошибку. Повторный CLI grant восстанавливает две demo-роли.

## Остановка и полный сброс

Остановить процессы и удалить только контейнеры и сеть демо, сохранив данные:

```powershell
pwsh -NoProfile -File scripts/Start-Demo.ps1 -Stop
```

Удалить также демо-БД, локальные ключи и файл учёток; следующий старт создаст всё заново:

```powershell
pwsh -NoProfile -File scripts/Start-Demo.ps1 -Stop -RemoveData
```

Остановка сверяет PID и время старта собственных процессов. Другие Compose projects
не затрагиваются. При ошибке после начала запуска сервисов собственные процессы/контейнеры убираются,
данные сохраняются для диагностики и повторной попытки. Логи: `secrets/demo/api.log`,
`api.err`, `web.log`, `web.err`. Одновременные start/stop защищены файловой блокировкой.
Остановка не закрывает браузер пользователя.

Сценарий повышает локальные auth rate limits для заполнения 12 учёток и включает
dev SMTP без TLS только на loopback. Все production defaults и бизнес-API остаются
без изменений. Страницы публичной активации/сброса ещё не реализованы до этапа 11.13;
для seed используется непосредственный API verify.

## Проверки сценария — 20.10.2026

Выполнены `dotnet restore Identity.slnx`, `dotnet tool restore` и
`dotnet build Identity.slnx --configuration Release --no-restore --disable-build-servers -m:1`:
0 warnings / 0 errors. Команда
`dotnet test tests/Identity.UnitTests --configuration Release --no-build --no-restore --disable-build-servers -m:1`:
147 passed, 0 failed, 0 skipped.

`pwsh -NoProfile -File scripts/Start-Demo.ps1 -NoBrowser` проверен на чистой реальной
PostgreSQL 17 / Mailpit и при повторном запуске. Все шесть миграций применяются;
повторный запуск не применяет новых миграций. Проверены native login/logout всех
12 учёток, HTTPS proxy, cookie/CSRF login/logout и dashboard обеих admin-ролей.
Сравнение хешей файлов подтвердило неизменность паролей при повторном запуске;
SQL-подсчёт подтвердил 12 пользователей с подтверждённым email, без дублей.

Проверены полный сброс через `-Stop -RemoveData` и последующее создание демо заново,
обычная остановка с сохранением данных и повторная остановка. Без `-Stop` параметр
`-RemoveData` отклоняется. Паролей seed-пользователей в сервисных логах нет;
файл с кредами исключён из Git. `git diff --check` и
`pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked` проходят.

Полные API/integration test suites, frontend lint/unit/Playwright suites и production
Docker build в рамках этой локальной утилиты не запускались. Новых миграций,
API-контрактов и изменений продуктового поведения нет; следующий этап не реализуется.
