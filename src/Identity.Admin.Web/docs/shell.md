# Identity Console: UI foundation (11.3)

Светлая тема, системный шрифт, нейтральные поверхности и один бирюзовый brand accent.
Цвета/шрифты/радиусы/spacing scale задаются через Ant Design tokens в `src/app/theme.ts`.
`cssVar` имеет фиксированный ключ identity-console; `app/styles.css` использует токены,
включая focus ring в порталах. Semantic state colors отделены от brand accent.
Нет тяжёлых теней, внешних шрифтов/изображений и другого UI kit. Ant Design App context
подготовлен для контекстных message/notification/modal, static APIs не используются.

## Layout и маршруты

AdminLayout: sticky sidebar240px, автоматическое сворачивание до80px на768–991px,
ручной toggle на desktop. До768px — drawer280px с закрытием по выбору меню, Escape
и mask click. Resize закрывает mobile drawer; возврат к mobile не открывает его снова.
Контейнеры ограничены80rem, spacing берётся из tokens. Есть headerContent/headerActions
slots для account/logout/breadcrumb, PageHeader с optional breadcrumbs/
description/actions и h1 страницы. С11.4 topbar показывает серверный профиль и выход.

| Route          | Текущее поведение                                  |
| -------------- | -------------------------------------------------- |
| `/`            | Replace redirect на `/admin`                       |
| `/admin`       | Dashboard с серверной статистикой (11.5)           |
| `/admin/users` | Каталог с серверными фильтрами и страницами (11.6) |
| `/admin/*`     | NotFound внутри admin shell                        |
| Прочие URL     | NotFound в PublicLayout с тем же branding          |

Dashboard и Users активны. Audit disabled и не имеет ссылки.
С11.4 `/admin/login` доступен публично; остальные admin paths закрыты AuthGate.
Login/logout/profile/Forbidden/cache cleanup: [authentication](authentication.md).
Метрики/UTC chart/refresh: [Dashboard](dashboard.md).
Каталог/URL filters/server pagination: [Users](users.md).
User details и `/account/*` ещё не реализованы. Нет фиктивных метрик/сессий.

## Общие компоненты

- LoadingState: screen reader status; EmptyState: явное отсутствие данных и optional
  action; ErrorState: только нормализованные сообщения и trace reference, произвольный
  exception.message не выводится; retry только по явному callback.
- ConfirmAction: controlled Modal с явными title/description/confirmText, Cancel в
  начальном фокусе, pending блокирует повторное подтверждение и закрытие. Компонент
  не отправляет запросы: permissions, pending/error, timeout и mutation принадлежат feature.
  Реальные формы действий не добавлены; callback проверяется только в test fixtures.
- StatusTag использует реальные UserStatus: PendingVerification, Active, Invited,
  Disabled, Deactivated. Незнакомое значение — нейтральный escaped текст, не выдуманный
  доменный статус. Подпись различает состояния без зависимости только от цвета.
- AppErrorBoundary перехватывает render errors, показывает безопасный fallback и
  ручной retry; сырые исключения не логирует.

## Клавиатура и фокус

Skip-to-content, semantic landmarks, aria labels у icon buttons, видимый focus,
reduced-motion media rule. Ant Design управляет первоначальным фокусом overlay;
`containTabFocus` предотвращает граничный Tab в browser chrome. Это ограниченное
использование DOM для focus management, без ручного изменения DOM/UI-состояния.
Возврат к mobile trigger выполняется после release focus lock. Drawer остаётся mounted
при быстром закрытии: destroyOnHidden мог прервать afterOpenChange до завершения enter.
ConfirmAction использует тот же Tab boundary и штатный return-focus Modal.

## Проверки

Из frontend:

```powershell
npm run typecheck
npm run lint
npm run format:check
npm run test -- --run
# Только при первой установке браузера:
npm exec playwright install chromium
npm run test:e2e
```

С11.4 browser smoke требует настоящий backend и cookie session. Из корня запустите
`pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1`: disposable PostgreSQL/Mailpit,
CLI bootstrap, HTTPS Vite/API, OpenAPI check, production build и Chromium.
Проверяются360/768/1280px, collapse/drawer, overflow/storage и только auth/dashboard reads в shell checks,
skip link, trap/Escape/return focus, resize,404 и SPA возврат. Login/logout/roles/
refresh/revocation входят в тот же прогон. Trace/video выключены, login screenshots
только до ввода credentials. Подробности и ограничения — [authentication](authentication.md).
`src/test/viewport.ts` — только jsdom shim для media queries; настоящую адаптивность
проверяет Chromium. В одном Modal unit test motion отключён тестовым ConfigProvider,
потому что jsdom не выполняет CSS animation. Production motion/focus checks не отключены.

После появления shell Vite сообщил о bundle816kB. React, Ant Design и UI primitives
выделены в библиотечные chunks (максимальный336kB); warning limit500kB не повышался.
Это cache boundaries, а не сокращение общего объёма загрузки. Новых dependencies нет.
С11.6 добавлена группа Ant Design Table; текущий максимальный chunk около357kB,
warning limit500kB сохранён. Каталог — отдельный lazy route, общие vendor chunks
могут загружаться вместе с оболочкой.
TypeScript/ESLint проверяют также Playwright config и e2e sources.

Настройки dev proxy, HTTPS, OpenAPI и безопасности описаны в [README](../README.md).
Production Docker/Compose/CI/CSP — этап12. Authenticated browser smoke реализован в11.4.
