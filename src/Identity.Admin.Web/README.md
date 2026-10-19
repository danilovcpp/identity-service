# Identity Console

Frontend самостоятельного продукта Identity. Выполнены **11.1 — bootstrap** и
**11.2 — HTTP-клиент, OpenAPI, CSRF**, **11.3 — тема и App Shell**,
**11.4 — Login / Logout / Route Guards**, **11.5 — Dashboard**, **11.6 — Users catalog**, **11.7 — User details**, **11.8 — Invite/resend**, **11.9 — Disable/enable** из
[дополнения v1.2](../../Identity_Admin_Codex_Addendum_v1.2.md).
Вход, выход, профиль и защита маршрутов работают через реальные cookie/CSRF API.
`/admin` показывает реальные метрики и график регистраций; `/admin/users` — серверный каталог с поиском, фильтрами и пагинацией. Audit пока не подключён.
Подробности: [authentication](docs/authentication.md), [UI foundation](docs/shell.md).
Контракт, обновление и проверки статистики: [Dashboard](docs/dashboard.md).
Параметры URL, границы страниц и проверки каталога: [Users](docs/users.md).
Карточка пользователя, copy ID и связанные security events: [User details](docs/user-details.md).
Приглашение пользователя и повторная отправка для Administrator: [Invitations](docs/invitations.md).
Блокировка/разблокировка с подтверждением, последствия и проверки: [Account status](docs/account-status.md).

## Требования и запуск

Node.js **22.13+ в ветке 22**, 24.x либо 26+; npm 10+. Проверено с Node.js 22.16.0
и npm 10.9.2. Используйте поддерживаемый Node LTS. Зависимости зафиксированы в
`package-lock.json`; не используйте `--legacy-peer-deps`.

Из корня репозитория:

```powershell
cd src/Identity.Admin.Web
npm ci
Copy-Item .env.example .env.local
npm run dev
```

Копируйте env-файл только при первом запуске, сохраняя свои локальные настройки.
Открыть [http://127.0.0.1:5173](http://127.0.0.1:5173). Vite слушает IPv4 loopback и не
выбирает другой порт молча. Для административного входа необходимы backend/БД,
включённый AdminAccess и HTTPS на обоих концах proxy (см. ниже).

## Команды

Все команды выполняются из этой директории:

Shell/theme/navigation и keyboard/browser checks: [UI foundation](docs/shell.md).
Для реального HTTPS smoke запустите из корня `pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1`.
Скрипт сам вызывает `npm run api:check` и `npm run test:e2e` в изолированном окружении.

| Команда                | Назначение                                                    |
| ---------------------- | ------------------------------------------------------------- |
| `npm ci`               | Воспроизводимая установка из lockfile                         |
| `npm run dev`          | Vite development server с React Fast Refresh                  |
| `npm run typecheck`    | TypeScript application + Vite config + tests                  |
| `npm run lint`         | ESLint, type-aware правила и React hooks, ноль предупреждений |
| `npm run format:check` | Проверка Prettier                                             |
| `npm run format`       | Форматирование исходников и конфигурации                      |
| `npm run test`         | Vitest в watch-режиме (в CI автоматически однократный запуск) |
| `npm run test:run`     | Однократный Vitest + React Testing Library в jsdom            |
| `npm run build`        | TypeScript + production-сборка в `dist/`                      |
| `npm run preview`      | Локальный просмотр `dist/` на порту 4173, без dev proxy       |
| `npm run api:generate` | Генерация TypeScript из реального OpenAPI Identity.Api        |
| `npm run api:check`    | Проверка drift без изменения файла типов                      |

Тесты проверяют реальный рендер `App`, Query provider/cache isolation и
HTTP-клиент через injected fake-fetch (только в тестах, не runtime fallback).
Playwright проверяет login/logout/refresh/roles и shell с настоящим backend через HTTPS.
Production dist собирается отдельно; browser smoke использует dev server с API proxy.

## Структура и границы

```text
src/
  app/          App, providers, QueryClient factory, theme, routes, layout CSS
  api/          typed HTTP-клиент, ошибки, query keys, generated schema.d.ts
  components/   layouts, PageHeader, states, confirmation, StatusTag
  features/auth/ session API, auth context/provider, login, guards, session controls
  features/dashboard/ read API, Query hook, metrics, registrations chart
  features/users/ catalog, details, scoped activity, invitation/status API/mutations/forms
  pages/        NotFoundPage
  test/         общий setup Vitest/RTL и очистка DOM между тестами
```

Импорты между каталогами — через `@/`, локальные стили/файлы могут использовать
относительный путь. Alias одинаков в TypeScript и Vite/Vitest. `strict` и
`noUncheckedIndexedAccess` включены и для app/tests, и для Vite config. Тесты
используют явные импорты Vitest, globals не включены. Бизнес-данных, заглушек входа,
фиктивных провайдеров авторизации нет. Ant Design 6 используется без второго UI kit;
theme tokens находятся в app/theme.ts. TanStack Query, ConfigProvider и Ant Design App
подключены через AppProviders; BrowserRouter находится в main.tsx.
Отдельного .NET проекта, backend-зависимостей и изменений solution нет.

## Dev proxy и HTTPS

`.env.example` содержит только серверные настройки Vite:

| Переменная                     | Значение по умолчанию / назначение                                |
| ------------------------------ | ----------------------------------------------------------------- |
| `IDENTITY_API_PROXY_TARGET`    | `http://localhost:5080`, текущий локальный профиль Identity.Api   |
| `IDENTITY_OPENAPI_URL`         | `http://127.0.0.1:5080/openapi/v1.json`, только type generation   |
| `IDENTITY_DEV_HTTPS_CERT_FILE` | Необязательный абсолютный путь к доверенному PEM certificate      |
| `IDENTITY_DEV_HTTPS_KEY_FILE`  | Необязательный абсолютный путь к соответствующему PEM private key |

`/api` проксируется без изменения пути в локальный Identity.Api; например,
`/api/v1/admin/auth/me` остаётся тем же route. Адрес API не задаётся в браузере.
Vite proxy работает только в `dev`; `preview` — просмотр статики, не production-host.
Backend запускать по [основному README](../../README.md); контракт доступен в
Development на `/openapi/v1.json`. Описание существующих API:
[admin access](../../docs/admin-access.md), [read](../../docs/admin-read-api.md),
[write](../../docs/admin-write-api.md). HTTP operations остаются в существующем API.

Для cookie-сценариев нужен **HTTPS на браузерном origin**, даже в dev:
backend требует Secure `__Host-` cookies и HTTPS для admin endpoints. Для локального
HTTPS укажите обе переменные файлов и откройте `https://127.0.0.1:5173`. Сертификат
должен быть доверен ОС/браузером и содержать IP SAN `127.0.0.1`; генерация/установка сертификатов
не выполняется приложением. Сертификаты и ключи не коммитить. Если backend использует
HTTPS, доверие к его сертификату также должно быть настроено для Node; проверка
сертификата proxy остаётся включённой (`secure: true`). Ошибки TLS не обходить.
HTTP startup подходит для bootstrap, он не подтверждает готовность cookie-auth.

Заголовок `X-Forwarded-Proto` от Vite не считается доверенным автоматически:
backend HTTPS listener либо явно настроенные по его runbook trusted proxy и HTTPS
обязательны. Не ослабляйте CSRF/CORS/cookie policy ради локального запуска.
Same-origin публикация, production Docker/reverse proxy/CSP/CI — этап 12.

## Конфигурация и секреты

`envPrefix: []` запрещает автоматическую публикацию env-переменных в browser bundle,
включая `VITE_*`; встроенные Vite `DEV`/`PROD`/`MODE` остаются доступны.
`loadEnv` читает `IDENTITY_*` только для серверного Vite config. Не добавляйте
`define: { 'process.env': ... }`, ключи или backend configuration в frontend.
Пароли, cookies, JWT, refresh/invitation/reset tokens нельзя сохранять в browser
storage, логах или analytics. CSRF request token живёт только в закрытой памяти клиента.
Нельзя размещать private keys, `.env` или секреты в `public/`: его содержимое Vite
копирует в `dist/` как есть.

`dist/`, `node_modules/`, локальные env, coverage и результаты тестов исключены из Git.
Никаких запросов к PostgreSQL/SMTP, фиктивных метрик и production fallback fixtures нет.

## Генерация API-контрактов

Запустите Identity.Api в Development по основному runbook. Из frontend:

```powershell
# Если используется другой локальный порт; значение также можно задать в .env.local.
$env:IDENTITY_OPENAPI_URL = 'http://127.0.0.1:5080/openapi/v1.json'
npm run api:generate
npm run api:check
```

`api:generate` читает реальный `/openapi/v1.json`, запускает openapi-typescript,
форматирует и записывает **src/api/schema.d.ts**, который хранится в Git.
`api:check` ничего не меняет и завершается ошибкой при drift или недоступном контракте.
Backend нужен только для генерации/проверки контракта; typecheck/test/build используют
сохранённый файл. Никаких локальных schema overlays или выдуманных DTO.
Проверка TLS включена; URL не должен содержать credentials, query или fragment.
Не передавайте генератору schema из недоверенного источника.

TypeScript закреплён на **5.9.3**: openapi-typescript7.13 требует peer TypeScript5.x.
Strict/noUncheckedIndexedAccess/type-aware ESLint сохранены, npm peer checks не обходятся.
Успешные200 для user details/sessions были пропущены в backend OpenAPI; с отдельного
разрешения пользователя добавлены только два ProducesResponseType attributes и
регрессия в существующем тесте. Маршруты/DTO/поведение endpoints не менялись.

Схема сохраняет точные серверные объявления: `number | string` для ряда чисел,
строковые status/role без enum, optional request fields, casing query (`Page` для
users/audit, `page` для sessions). Backend по-прежнему валидирует обязательные значения.
Публичные Auth paths в OpenAPI имеют uppercase `Auth`; ASP.NET route matching
нечувствителен к регистру. Generated types не править вручную.

## HTTP-клиент и отмена

Единственный runtime fetch находится в `src/api/httpClient.ts`. Используйте клиент
из будущих `features/<feature>/api`, не из UI. Метод, route, path/query/body и результат
выводятся из generated OpenAPI; нет caller-supplied generic DTO или произвольных headers.
В scope клиента входят admin endpoints и два публичных account flows. Native JWT
login/refresh не являются браузерной авторизацией этой консоли.

```ts
import { apiClient } from '@/api/httpClient';

const controller = new AbortController();
const users = await apiClient.request('get', '/api/v1/admin/users', {
  query: { Page: 1, PageSize: 25, Search: 'example', EmailVerified: true },
  signal: controller.signal,
});
// users.items типизирован; при уходе со страницы вызывающая сторона отменяет запрос.
controller.abort();

const details = await apiClient.request('get', '/api/v1/admin/users/{userId}', {
  path: { userId: '00000000-0000-0000-0000-000000000001' },
  signal: AbortSignal.timeout(10_000),
});
```

Передавайте `signal` из TanStack Query function или caller-owned AbortController.
Таймаут задаётся вызывающей стороной через AbortSignal.timeout; можно объединить
его с отменой через AbortSignal.any. Клиент не вводит скрытый таймер или retry.
Все запросы same-origin, `credentials: 'same-origin'`, no-store и `redirect: 'error'`:
неожиданный redirect не отправит JSON с паролем/token на другой адрес.
Path parameters кодируются, query сохраняет false/0 и пропускает null/undefined.
Пустой204 не читается; пустой202 возвращает undefined. HTML вместо успешного JSON,
сломанный JSON или пустой200 дают ClientError. Полная runtime DTO-валидация не добавлена:
успешный JSON доверяет сгенерированному backend-контракту.

`ApiError` содержит authoritative HTTP status, machine code, traceId и fieldErrors.
Документированные ProblemDetails extensions проверяются runtime, поскольку стандартная
OpenAPI ProblemDetails schema не перечисляет code/traceId/errors. Сырые detail/instance,
неизвестные metadata, Response/Request, URL и исходная fetch exception не сохраняются.
Сообщение ошибки локальное/стабильное; fieldErrors предназначены для Ant Design Form.
`ClientError.kind`: network, aborted, timeout, invalid_response или invalid_request.
HTTP401/403/409/429/5xx сохраняют статус даже при пустом/не-JSON error body.
Ни клиент, ни QueryClient не повторяют неуспешные команды. HTTP401 не делает redirect
или скрытый login; session clearing/navigation/Forbidden UI — отдельный этап11.4.

## CSRF и Query cache

Для unsafe admin requests клиент лениво получает **GET /api/v1/admin/auth/csrf**
с реальным ответом `{token}`, затем отправляет `X-CSRF-TOKEN`. Это включает
login/logout; GET не запрашивает CSRF. Публичные invitation/reset POST не требуют
admin CSRF, как и существующий backend. Cookies остаются HttpOnly и управляются браузером.

`apiClient.prepareCsrf({ signal })` заранее получает token и возвращает void.
Сам endpoint исключён из публичного request API, чтобы token не попал в Query cache.
Параллельные команды делят acquisition; отмена одного caller не отменяет остальные,
последний отменённый caller прерывает незавершённый CSRF fetch.
После успешных login/logout token очищается (он связан с identity); HTTP401 или
csrf_invalid также очищают его. Новая попытка возможна только отдельным явным вызовом,
никогда как повтор уже отправленного POST/DELETE. `clearCsrfToken()` доступен для
явной смены сессии; незавершённая acquisition тоже очищается.

`createApiClient(injectedFetch)` даёт изолированный клиент в тестах. Production export
apiClient не выполняет I/O при импорте и не использует storage, analytics или console.
Если запрос прерван после отправки команды, сервер уже мог её применить: UI должен
обновить данные/проверить результат, а не повторять небезопасный запрос автоматически.

`createQueryClient()` создаёт свежий cache, AppProviders сохраняет его на время mount.
Retry отключён у queries/mutations. Mutations имеют networkMode:always, поэтому
не ставятся в скрытую offline-очередь; gcTime0 удаляет неактивные mutations.
Query keys централизованы в `src/api/queryKeys.ts`: auth.me/dashboard/users/detail/
sessions/audit с параметрами фильтрации и prefix keys для точечной invalidation.
Не помещайте credentials, CSRF/one-time tokens или пароли в keys, query data,
mutation variables, persisted cache или devtools. Sensitive account/login calls должны
передавать секреты напрямую через клиент, а кешировать можно лишь безопасный результат.
AppProviders отвечает за Query/Ant Design; AuthProvider в routing branch выполняет
`/auth/me`, управляет auth state и очисткой кеша. См. [authentication](docs/authentication.md).

Ссылки на официальные инструменты:
[openapi-typescript Node API](https://openapi-ts.dev/node),
[TanStack Query retries](https://tanstack.com/query/latest/docs/framework/react/guides/query-retries),
[TanStack mutations](https://tanstack.com/query/latest/docs/framework/react/guides/mutations).
