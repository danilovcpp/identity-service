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
slots для будущих account/logout/breadcrumb, PageHeader с optional breadcrumbs/
description/actions и h1 страницы. Topbar не имитирует вошедшего администратора.

| Route      | Текущее поведение                               |
| ---------- | ----------------------------------------------- |
| `/`        | Replace redirect на `/admin`                    |
| `/admin`   | Стартовый workspace без метрик и backend данных |
| `/admin/*` | NotFound внутри admin shell                     |
| Прочие URL | NotFound в PublicLayout с тем же branding       |

Console — единственный активный пункт. Users/Audit disabled и не имеют ссылок.
`/admin/login`, user details и `/account/*` пока не реализованы.
Shell доступен анонимно: он содержит только публичный branding и основу UI, не admin
данные. Это не auth guard и не обход backend. Login/logout/profile/guards/Forbidden — 11.4.
Никаких фиктивных accounts, сессий, метрик или полей, которых нет в API.

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

`test:e2e` собирает production dist и запускает Playwright с отдельным loopback preview
на5174, который завершается после тестов. Backend/PostgreSQL не нужны для shell smoke.
reuseExistingServer=false не подменяет тесты чужим сервером. Проверяются360/768/1280px,
collapse/drawer, no overflow/storage/API calls, skip link, trap/Escape/return focus,
resize, admin/public404 и SPA возврат. Скриншоты только несекретной оболочки пишутся
в ignored test-results; trace/video выключены. Это не authenticated/API+UI E2E.

`src/test/viewport.ts` — только jsdom shim для media queries; настоящую адаптивность
проверяет Chromium. В одном Modal unit test motion отключён тестовым ConfigProvider,
потому что jsdom не выполняет CSS animation. Production motion/focus checks не отключены.

После появления shell Vite сообщил о bundle816kB. React, Ant Design и UI primitives
выделены в библиотечные chunks (максимальный336kB); warning limit500kB не повышался.
Это cache boundaries, а не сокращение общего объёма загрузки. Новых dependencies нет.
TypeScript/ESLint проверяют также Playwright config и e2e sources.

Настройки dev proxy, HTTPS, OpenAPI и безопасности описаны в [README](../README.md).
Production Docker/Compose/CI/CSP и authenticated browser scenarios — последующие этапы.
