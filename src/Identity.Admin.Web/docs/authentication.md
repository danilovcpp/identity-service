# Identity Console: authentication (11.4)

`/admin/login` — публичная форма, остальные `/admin` и `/admin/*` закрыты AuthGate.
Проверка `/api/v1/admin/auth/me` выполняется при старте, возврате фокуса и remount;
сервер остаётся источником сессии и роли. Пока нет ответа, shell не монтируется.
401 ведёт на login; 403 показывает Access denied с явной проверкой доступа и выходом.
Network/503 дают безопасную ошибку с ручным GET retry. Redirect loop отсутствует.
Публичные NotFound доступны независимо от результата проверки сессии.

## Поток и состояние

- `features/auth/api.ts` использует только общий typed client. В кеш допускаются
  только id/email/displayName/role из `/me`; неизвестная роль отклоняется.
- AuthProvider и useAuth предоставляют профиль и `canWrite` только для
  IdentityAdministrator. IdentityReadOnly получает доступ к чтению; write UI пока нет.
  Проверка UI capability не заменяет серверную авторизацию.
- Login передаёт пароль напрямую клиенту, без Query mutation/variables или global state.
  Form очищает пароль после попытки, submit блокируется ref и pending. Email проверяется;
  пароль не trim и не проверяется новой политикой сложности при входе.
- После POST login выполняется GET me, подтверждающий cookie. Старые reads отменяются,
  кеш предыдущей личности и CSRF очищаются, безопасный профиль записывается в Query.
- Return destination хранится в router state: только `/admin` и простые внутренние
  сегменты, без login, absolute URL, fragment, backslash и percent encoding в path.
  Обычно вход ведёт на `/admin`; после guard redirect — на сохранённый внутренний путь.
  `/admin/users` и с11.7 `/admin/users/:userId` с валидным UUID принимают нормализованные query-фильтры из [allowlist каталога](users.md);
  остальные query-параметры отбрасываются. Browser storage для return path не используется.
- Logout ждёт серверный 204 (401 также означает завершённую сессию), затем отменяет
  queries, очищает Query/Mutation cache и CSRF. Network/CSRF ошибка не объявляется
  успешным выходом: сессия остаётся, показана ошибка для ручного повтора.
- Центральный client уведомляет AuthProvider о прочих admin 401/403. 401 завершает UI
  сессию; 403 закрывает контент без redirect. Login и me имеют собственную обработку;
  csrf_invalid остаётся ошибкой конкретного запроса. Отменённый старый запрос не
  может завершить новую сессию поздним 401. Все Query функции должны передавать signal.
- Авторизационные операции имеют timeout30s и отмену при unmount. Никакого replay
  credentials/unsafe методов. После неоднозначного network timeout сервер мог завершить
  операцию: повторная проверка/refresh страницы восстанавливает фактическую cookie-сессию.

Никаких JWT, чтения HttpOnly cookies, browser storage, persisted Query cache или
логирования credentials. CSRF — в закрытой памяти общего клиента. Login error generic;
server detail/raw exception не выводятся. Topbar показывает настоящий профиль/роль и Sign out.
Тема/отступы наследуются; мобильная шапка не переносит служебный заголовок поверх профиля.

## Локальная разработка и проверки

Обычный dev: выполните [admin runbook](../../../docs/admin-access.md), включите HTTPS
listener API и AdminAccess с защищённым Data Protection directory, назначьте роль
зарегистрированному и verified пользователю через CLI. Укажите PEM certificate/key
для Vite и HTTPS API target в `.env.local`, настройте доверие Node к сертификату API.
Серверы должны использовать один браузерный origin через proxy. Не отключайте secure
cookies, CSRF, CORS или TLS validation для ручного запуска.

Для воспроизводимого локального smoke из корня, после npm ci и Release build:

```powershell
dotnet tool restore
# При первой установке Playwright:
cd src/Identity.Admin.Web
npm exec playwright install chromium
cd ../..
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
```

Нужны Windows PowerShell7.4+, .NET10, Node22/npm, Docker Linux containers и свободные
loopback ports. Скрипт создаёт уникальный Compose project с PostgreSQL17/Mailpit,
применяет шесть существующих миграций, генерирует ephemeral keys/certificate и пароль,
регистрирует два аккаунта через REST, подтверждает их через доставленное письмо и
назначает Administrator/ReadOnly существующим CLI. Все секреты только в окружении и
Git-ignored временном secrets directory; настройки окружения восстанавливаются.

Затем проверяет OpenAPI drift, production build и Chromium tests через отдельный
Vite HTTPS dev server. Cookie HttpOnly/Secure/Strict проверяется browser API, не JS;
refresh/login/logout/revocation, generic errors, routes, keyboard и360/768/1280 layout.
Проверяется отсутствие тестовых секретов в dist. Trace/video и storage export выключены;
снимки login выполняются до ввода credentials. Проверка browser certificate отключена
только в изолированном runner; Vite→API TLS проверяется с NODE_EXTRA_CA_CERTS.
Глобальное доверие сертификатам и production конфигурация не меняются.
Own API process/Compose volumes/temporary keys и dev server удаляются после прогона.
При аварийном завершении процесса оператору следует удалить только оставшийся project
с префиксом identity-admin-web и соответствующий secrets/admin-web directory.

Vitest использует fake fetch только в тестах: success/errors/validation/double submit,
profile projection, 401/403, unknown role/readOnly, cache cleanup, logout failure,
safe return paths и late aborted responses. `npm run test -- --run`, typecheck/lint/
format:check/build остаются самостоятельными командами. Нет production fixtures.

Dashboard реализован в [11.5](dashboard.md), каталог Users — в [11.6](users.md), карточка — в [11.7](user-details.md), invite/resend — в [11.8](invitations.md); audit/остальные actions/account pages — последующие11.x; production hosting,
Docker image frontend, proxy/CSP/CI и unrestricted internet access — отдельный этап12.
До MFA требуется ограниченный сетевой контур. Никаких новых backend API/миграций.
