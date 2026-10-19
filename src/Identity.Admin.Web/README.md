# Identity Console

Frontend самостоятельного продукта Identity. Выполнен **11.1 — bootstrap** из
[дополнения v1.2](../../Identity_Admin_Codex_Addendum_v1.2.md).
На стартовой странице только название приложения: административные экраны и запросы
к API появятся в следующих отдельных подэтапах.

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
выбирает другой порт молча. Для стартовой страницы backend/БД/Docker не требуются.

## Команды

Все команды выполняются из этой директории:

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

Один smoke-тест проверяет рендер настоящего `App` с Ant Design и доступным `h1`.
Playwright установлен как инструмент будущих сценариев; E2E-конфигурация и сценарии
появятся вместе с пользовательскими потоками. В 11.1 Playwright-тестов нет.

## Структура и границы

```text
src/
  app/          App, базовые стили; позже providers/theme/routing
  api/          будущий централизованный HTTP-клиент и generated types (11.2)
  components/   будущие общие UI-компоненты (11.3)
  features/     будущие вертикальные срезы с API/hooks/UI
  pages/        страницы, если потребуются
  test/         общий setup Vitest/RTL и очистка DOM между тестами
```

Импорты между каталогами — через `@/`, локальные стили/файлы могут использовать
относительный путь. Alias одинаков в TypeScript и Vite/Vitest. `strict` и
`noUncheckedIndexedAccess` включены и для app/tests, и для Vite config. Тесты
используют явные импорты Vitest, globals не включены. Бизнес-данных, заглушек входа,
провайдеров авторизации, API-клиента и маршрутов пока нет. Ant Design 6 используется
без второго UI kit; централизованные theme tokens и App Shell — отдельный этап 11.3.
React Router и TanStack Query установлены, но до соответствующих этапов не подключены.
Отдельного .NET проекта, backend-зависимостей и изменений solution нет.

## Dev proxy и HTTPS

`.env.example` содержит только серверные настройки Vite:

| Переменная                     | Значение по умолчанию / назначение                                |
| ------------------------------ | ----------------------------------------------------------------- |
| `IDENTITY_API_PROXY_TARGET`    | `http://localhost:5080`, текущий локальный профиль Identity.Api   |
| `IDENTITY_DEV_HTTPS_CERT_FILE` | Необязательный абсолютный путь к доверенному PEM certificate      |
| `IDENTITY_DEV_HTTPS_KEY_FILE`  | Необязательный абсолютный путь к соответствующему PEM private key |

`/api` проксируется без изменения пути в локальный Identity.Api; например,
`/api/v1/admin/auth/me` остаётся тем же route. Адрес API не задаётся в браузере.
Vite proxy работает только в `dev`; `preview` — просмотр статики, не production-host.
Backend запускать по [основному README](../../README.md); контракт доступен в
Development на `/openapi/v1.json`. Описание существующих API:
[admin access](../../docs/admin-access.md), [read](../../docs/admin-read-api.md),
[write](../../docs/admin-write-api.md). HTTP operations остаются в существующем API.

Для последующих cookie-сценариев нужен **HTTPS на браузерном origin**, даже в dev:
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
storage, логах или analytics. В 11.1 нет операций с чувствительными данными.
Нельзя размещать private keys, `.env` или секреты в `public/`: его содержимое Vite
копирует в `dist/` как есть.

`dist/`, `node_modules/`, локальные env, coverage и результаты тестов исключены из Git.
Никаких запросов к PostgreSQL/SMTP, фиктивных метрик и production fallback fixtures нет.
