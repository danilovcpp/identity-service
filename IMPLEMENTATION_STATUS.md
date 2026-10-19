# Текущий статус: этап 11.4 завершён

**Completed: 11.4**

**Next: 11.5 — только по отдельному запросу пользователя.**

## 1. Этап

**11.4 — Administrative Login / Logout / Route Guards**, 2026-10-19.
Прочитаны AGENTS.md, Identity_Codex_Spec.md, дополнение v1.2, текущие API/controllers,
frontend и status. Начальное дерево чистое;11.3 подтверждён commit **ade6b81**.
Реализован только11.4. Backend0–10 и архитектура11.1–11.3 сохранены.

## 2. Реализовано

- `src/Identity.Admin.Web/src/features/auth/api.ts`: реальные typed login/logout/me,
  безопасная проекция профиля, fail-closed для неизвестных ролей. После login GET me
  подтверждает cookie и даёт актуальные capabilities.
- `AuthProvider.tsx`, `authContext.ts`: Query session, loading/authenticated/
  unauthenticated/forbidden/error, server profile, canWrite только Administrator.
  Начальная проверка, refetch при возврате фокуса; нет самодельного таймера срока cookie.
  Reads отменяются перед сменой личности, Query/Mutation cache и CSRF очищаются при
  logout/401; при forbidden business cache удаляется. Login/logout не Query mutations:
  password не попадает в mutation variables/cache/devtools. Timeout30s, unmount abort.
- `LoginPage.tsx`: Ant Form email/password, validation, generic login failures,
  safe network/503/429/CSRF errors, pending/ref double-submit guard, очистка password
  после каждой попытки. Нет новых правил сложности пароля для входа.
- `AuthGate.tsx`, `returnPath.ts`, routing: все admin paths, включая404, закрыты
  session guard; login публичный. Loading/error/Forbidden не монтируют shell.
  401 redirect с безопасным pathname в router state;403 без redirect loop с GET retry.
  Absolute URL/query/fragment/backslash/encoded return targets/login loops запрещены.
- `SessionControls.tsx`, styles: настоящий displayName/email, роль и CSRF logout.
  Network/CSRF failure выхода не считается успехом; явная ошибка/ручной повтор.
  Существующие headerActions slots использованы без переписывания AdminLayout.
  Адаптация шапки360px, CSS ellipsis без дополнительного observer/state.
- `api/httpClient.ts`: минимальная подписка владельца сессии на admin401/403;
  login/me обрабатываются отдельно, csrf_invalid не становится Forbidden, поздний
 401 отменённого запроса не завершает новую сессию. Fetch остаётся централизованным.
- Unit/component/transport tests; прежние shell tests теперь ждут настоящий auth state.
  Test fixtures только в test/e2e; production mock responses отсутствуют.
- `scripts/Test-AdminWebSmoke.ps1`, Playwright config, authFixture/auth/shell specs:
  disposable PostgreSQL17/Mailpit, шесть миграций, TLS Vite/API, временные secrets,
  REST registration + delivered email verification + CLI bootstrap двух ролей.
  OpenAPI drift/build/browser checks; cleanup своих процессов/Compose/keys и restore env.
  Сертификат доверяется Node через NODE_EXTRA_CA_CERTS, глобальный trust не меняется.
- README/CHANGELOG/frontend README, docs/shell.md и новый docs/authentication.md.
  Новых dependencies, package/lockfile, API/DTO/OpenAPI/backend/security policy/DB
  изменений нет. Миграции, production Docker/CI, версии продукта и env samples прежние.

## 3. Решения

Сессия хранится в серверной HttpOnly cookie; Query содержит только безопасный профиль.
Auth context — производное состояние Query и capability, не отдельная копия identity.
Пароль живёт в Form и параметре короткой async операции, затем очищается; никакой
persisted state, token parsing или client-side expiry timer. Ошибка сети при logout
оставляет session UI до подтверждения сервера;401 завершает уже недействующую сессию.

По обычному login переход на /admin. После guard redirect возвращается сохранённый
внутренний pathname (требование11.4 п.2); query/hash намеренно не переносятся до
появления соответствующих фильтров и allowlist. Users/Audit по-прежнему disabled;
Dashboard и CRUD не реализованы. Неизвестный admin path после auth показывает404.

Browser smoke теперь требует настоящий backend: build проверяется отдельно, Chromium
работает с Vite dev HTTPS proxy. Это локальная тестовая инфраструктура11.4, не
production packaging этапа12. Browser ignoreHTTPSErrors ограничен disposable fixture;
проверка TLS proxy к API остаётся включённой. Ручной dev run требует доверенный cert.

## 4. Проверки

### Commands run

Из frontend:

```powershell
npm run format
npm run typecheck
npm run lint
npm run format:check
npm run test -- --run
npm run api:check
npm run build
npm run test:e2e
```

Последние три команды запускались внутри HTTPS fixture (build — через test:e2e).
Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage11-4'
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

Fixture дополнительно выполняет `docker compose up --detach --wait postgres mailpit`,
`dotnet ef database update`, existing AdminCli grant для двух ролей, HTTPS API process,
Vite dev server и в finally `docker compose down --volumes --remove-orphans`.

### Observed results

- TypeScript/lint/Prettier/build exit0. **94 frontend tests passed**,0failed/skipped,
 7files. Auth success/errors/validation/double submit,401/403/readOnly/unknown role,
  cache cleanup/logout failure/manual GET retry, safe return targets, memory-only CSRF,
  listener unsubscribe/late cancelled response, прежние transport/shell/components.
- **10 Chromium tests passed**, без retries: реальные login/logout обеих ролей,
  cookie refresh после reload, generic invalid credentials, anonymous redirect,
  server revocation с повторным предъявлением прежнего cookie,360/768/1280 shell/login,
  keyboard skip link/Tab trap/Escape/return focus, resize,404/SPA navigation.
  Browser API проверяет HttpOnly/Secure/Strict; JS cookie/storage не содержит auth.
- OpenAPI types match Identity.Api. Шесть существующих миграций применены к пустой
  disposable DB, pending model changes отсутствуют; новых миграций нет.
- Production build: app101.27kB, React218.76kB, Antd406.99kB, primitives182.01kB;
  chunk warning отсутствует. Проверка dist на временные password/DB/PFX secrets проходит.
  Косметический NO_COLOR/FORCE_COLOR warning среды runner остаётся.
- .NET restore/build/format exit0,0warnings/0errors; **470 tests passed**,0failed/skipped:
  Unit147 + API45 + Integration278 с настоящими PostgreSQL/Mailpit/Docker.
- Визуально просмотрены login360/1280 и shell360; Chromium проверяет также768 и1280.
  Login snapshots сняты до credentials, trace/video/storage export выключены.
- Secret guard self-tests и tracked/untracked tree, git diff --check проходят.
  Secrets/fixture containers/volumes/API/dev server очищены после прогонов.

Промежуточные ошибки исправлены: sandbox EPERM требовал повторного запуска tool вне
sandbox; после split context исправлены type/lint ошибки. Unit tests выявили
ResizeObserver dependency у Typography ellipsis (заменено CSS), accessible icon/loading
names и слишком общий status selector. Первый smoke не выбирал уже доставленное
Mailpit письмо; fixture теперь последовательно использует и очищает свой mailbox.
Keyboard smoke ждал Tab раньше async session check; добавлено ожидание shell.
Визуально обнаружено переполнение мобильной шапки по высоте — layout исправлен.
Все окончательные команды выше проходят; непрошедшие попытки не выдаются за успех.

## 5. Безопасность

Без JWT/localStorage/sessionStorage/persisted Query cache, чтения cookie в production JS,
новых ролей/серверных проверок. Login/logout используют прежние secure cookies/CSRF.
Passwords не кешируются/не логируются/не передаются через URL; default accounts только
в disposable тестах со случайным паролем. Rate limiting и server revocation сохранены.
Unknown role fail-closed;401 очищает данные,403 не зацикливает redirect; return paths
не позволяют open redirect. Backend остаётся единственным источником прав.

## 6. Ограничения / Known issues

Блокеров11.4 нет. Бизнес-метрики, Users/Audit/actions и публичные account forms — будущие
подэтапы. Browser tests пока Chromium, dev HTTPS proxy; production dist собран, но
production proxy/Docker frontend/TLS/CSP/CI/axe/Lighthouse не проверялись и относятся
к дальнейшей приёмке. Docker image API smoke отдельно не повторялся: backend/image
не менялись; Docker integration + реальный HTTPS API/frontend smoke выполнены.
Новых dependencies нет, npm ci/audit в11.4 не повторялись. До MFA доступ ограниченным
сетевым контуром обязателен. При crash runner ephemeral resources требуют ручной очистки.

## 7. Следующий этап

**11.5 — Dashboard со статистикой**, только по отдельному запросу. Не начат.

---

# Предыдущий отчёт: этап 11.3
# Текущий статус: этап 11.3 завершён

**Completed: 11.3**

**Next: 11.4 — только по отдельному запросу пользователя.**

## 1. Этап

**11.3 — UI-тема, навигация и адаптивный App Shell**, 2026-10-19.
Изучены AGENTS.md, Identity_Codex_Spec.md, Identity_Admin_Codex_Addendum_v1.2.md,
текущий frontend/API status, UserStatus и Git. Начальное дерево чистое;
11.2 подтверждён commit **600159a**, 11.1 — **b296d60**.
Реализован только11.3, без authentication, backend данных и форм действий.

## 2. Реализовано

- `app/theme.ts`, AppProviders: единый Ant Design ConfigProvider, светлая тема,
  нейтральные поверхности, бирюзовый accent, системный шрифт, tokens цветов/типографики/
  размеров/радиусов. Ant Design App context для будущих context-aware notifications.
  CSS variables имеют фиксированный key identity-console, включая portal overlays.
- `components/layout/AdminLayout`: sticky sidebar240px, collapsed80px на768–991px,
  ручной desktop toggle; mobile<768 — drawer280px. Topbar/headerContent/headerActions
  slots, main content max80rem и единые token-based отступы в `app/styles.css`.
  Профиль/роль/logout не имитируются. `Brand`, `Navigation`, `PublicLayout` отдельные.
- `app/routes.tsx`, main.tsx: BrowserRouter, `/` replace ->`/admin`; ConsolePage index,
  admin wildcard404 внутри shell, global/public wildcard404 в PublicLayout.
  Единственный активный пункт — Console. Users/Audit disabled, без href/бизнес-страниц.
  `/admin/login` и `/account/*` не активированы до соответствующих этапов.
- Общие PageHeader (h1, breadcrumb/description/actions slots), LoadingState (status),
  EmptyState (optional action), ErrorState (safe messages/trace/manual retry),
  controlled ConfirmAction Modal (explicit title/text/pending/Cancel), StatusTag.
  StatusTag сопоставляет только пять реальных backend states; неизвестное значение
  отображается нейтральным escaped текстом, не новым доменным состоянием.
- AppErrorBoundary: render fallback/manual retry без сырой ошибки; production root
  onCaughtError не печатает caught exception в console. Backend error adapter сохранён.
- Skip-to-content, landmarks, icon button labels, декоративные menu icons скрыты
  от screen reader, видимый focus в app/portals, reduced-motion CSS.
  `containTabFocus` закрывает boundary Tab внутри overlays; initial focus — Ant Design.
  Drawer close возвращает focus на trigger после release lock; resize не восстанавливает
  stale open state. Ограниченные DOM reads/focus нужны только accessibility.
- `e2e/shell.spec.ts`, playwright.config.ts, test:e2e script: Chromium production
  preview на отдельном loopback5174, build перед тестом, no server reuse, no trace/video.
  tsconfig.node/ESLint включают config и E2E source; jsdom media/scrollbar shims в test/.
- Vite vendor chunks React/Ant Design/UI primitives после наблюдённого816kB warning;
  максимум336kB, warning limit500kB не повышен. Это cache boundaries, не уменьшение
  суммарной загрузки. Новых dependencies нет, lockfile/версии сохранены.
- README/CHANGELOG/frontend README и `docs/shell.md` обновлены.
  `.gitkeep` из components/pages удалены. API client/generated types/backend/API/DB/
  migrations/security policies/env samples/Docker/CI не менялись.

## 3. Решения

Полезная основа без фиктивного Dashboard: индекс содержит branding/empty workspace,
без статистики, сетевых запросов, demo accounts и мутаций. Shell пока анонимный,
потому что содержит только публичный каркас. Это не авторизация и не обещание готовых
protected pages. Настоящие login/logout/guards/session cache cleanup —11.4.

Router branches подготовлены, будущие страницы не включены ради navigation demo.
Header identity/logout подключаются через slots после real auth; shared components
не импортируют feature hooks и не вызывают fetch. ConfirmAction — presentation,
permissions/mutation/error/timeout остаются у будущей feature. State локальный.

Стили централизованы: Ant tokens для paint/design values, общая CSS для geometry/
responsive/focus; нет второго UI kit, внешних шрифтов/изображений или page overrides.
Native React refs и DOM focus используются только для keyboard accessibility.
Drawer без destroyOnHidden: быстрое закрытие до конца enter иначе прерывало callback
и теряло return focus. Подробности и контракты компонентов — `docs/shell.md`.

## 4. Проверки

### Commands run

Из `src/Identity.Admin.Web`:

```powershell
npm run dev
npm run format
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run test -- --run
npm run build
npm run test:e2e
```

Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage11-3'
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
node TestResults/stage11-3-visual.mjs
node TestResults/stage11-3-focus.mjs
node TestResults/stage11-3-drawer.mjs
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- typecheck/lint/format:check/build exit0. **69 frontend unit/component tests passed**,
  0 failed/skipped (48 прежних +21 UI/routes); six files. Проверены states, real status
  labels/escaping, safe errors, explicit retry/confirm/pending/cancel, render fallback,
  navigation/collapse/mobile close, disabled links,404/back и QueryClient isolation.
- **6 Playwright Chromium tests passed**, production dist, без retry:
  360/768/1280px, overflow/storage/API-call checks; menu/collapse/drawer; keyboard
  skip link/Tab trap/Escape/return focus; resize; admin/public404 и SPA возврат без reload.
  Визуально просмотрены все три размера и открытый mobile drawer. Screenshots в ignored
  test-results; dev diagnostic scripts/images в ignored TestResults.
- Final build chunks: app82.19kB, React218.76kB, Antd336.14kB, primitives179.42kB;
  Vite chunk warnings отсутствуют. Runner выводит косметический warning среды
  FORCE_COLOR/NO_COLOR; тесты exit0, runtime page errors отсутствуют.
- .NET restore/build/test/format exit0: **0 warnings/0 errors**, **470 passed**, 0
  failed/skipped: Unit147 + API45 + Integration278, реальные PostgreSQL/Mailpit/Docker.
  EF model check exit0, pending model changes отсутствуют; новых миграций нет.
- Secret guard self-tests/current tracked+untracked tree и diff --check проходят.
  Новых package versions нет; npm install/ci/audit в11.3 не повторялись.

Промежуточные ошибки не скрыты: первый unit run —2 failures (декоративные icon names
и jsdom animation visibility); затем оставался Modal visibility. Исправлены aria-hidden,
ByRole query typing и test-only motion override; production motion не отключался.
Первые browser runs ловили boundary Tab ->browser chrome и lost return focus при
быстром Escape. Добавлены Tab boundary, trigger ref, return-focus после release,
удалён преждевременный drawer unmount; все проверки в окончательном run прошли.
Первый bundle816kB (затем514kB после двух chunks) давал warning; третья vendor group
устранила предупреждение, порог не повышался.
Во время перезаписи Vite config форматтером наблюдались transient HMR reload errors;
свежий запуск окончательного config проверен: ready206ms. Временные dev/preview
серверы после проверок остановлены.

## 5. Безопасность

Нет backend данных, passwords/tokens/roles из фиктивного источника или новых API calls.
Shared ErrorState/boundary не показывают произвольные exception details; JSX экранирует
текст статусов. API cookie/CSRF/role checks сохранены. Tokens не попадают в storage,
query keys, screenshots или DOM; тесты проверяют пустой storage и отсутствие API calls.
Navigation не даёт доступа к будущим sections; backend остаётся источником authorization.

## 6. Ограничения / Known issues

Блокеров11.3 нет. Auth/guards/identity/logout/Forbidden и бизнес-экраны — последующие
этапы. Текущая оболочка сама по себе не является готовой защищённой админкой.
Полная проверка Modal focus с реальными actions относится к будущим feature E2E;
здесь Modal callbacks/pending проверены component tests, drawer — реальным browser.
Automated axe/Lighthouse, production HTTPS/CSP/VPN/MFA/SMTP, full account E2E и remote CI
не запускались. Separate Docker image smoke не повторялся: backend/container configs
не менялись; frontend hosting/Docker/CI —12. Реальный Docker использован integration suite.

Config/env/API/migrations compatibility сохранена, требований backend не расширено.
Commit/tag/publish/deploy/внешние accounts/messages не выполнялись.

## 7. Следующий этап

**Next: 11.4 — административные Login / Logout / Route Guards.**
Не начат; продолжение только по отдельному запросу пользователя.

---

# Исторический отчёт: этап 11.2

**Completed: 11.2**

**Next: 11.3 — только по отдельному запросу пользователя.**

## 1. Этап

**11.2 — Типизированный HTTP-клиент, OpenAPI, CSRF**, 2026-10-19.
Источники: AGENTS.md, Identity_Codex_Spec.md, Identity_Admin_Codex_Addendum_v1.2.md
(§2, §8/11.2, §9), предыдущий статус и текущие Controllers/DTO/configuration.
Дерево на старте чистое. Этап11.1 подтверждён **b296d60**, backend10 — **c87306d**.

Обнаружен конкретный пробел OpenAPI: GET users/{userId} и /sessions содержали только404,
без200 и схем AdminUserDetails/AdminUserSession. До исправления получено **явное
разрешение пользователя** добавить только два ProducesResponseType(200) attributes.
Исключение к запрету backend-изменений согласовано, бизнес-логика/ответы не менялись.

## 2. Реализовано

- `scripts/generate-api.mjs`, команды **api:generate / api:check**, dev-only
  openapi-typescript7.13.0; `src/api/schema.d.ts` сгенерирован из живого Identity.Api
  OpenAPI3.1.1, хранится в Git. Нет локальных schema overlays/ручных DTO/runtime SDK.
- Generator проверяет наличие успешных контрактов details/sessions, форматирует вывод,
  не пишет файл при ошибке загрузки/валидации. api:check сравнивает точный generated output.
  IDENTITY_OPENAPI_URL — только Node/tooling env, не browser config.
- `src/api/contracts.ts`: вывод методов/path/query/body/success response из generated
  paths. Доступны admin endpoints и public invitation acceptance/password reset;
  native token protocol не используется для браузерной авторизации.
- `src/api/httpClient.ts`: injectable Fetch factory + production apiClient, relative
  same-origin paths, credentials:same-origin, JSON, no-store, redirect:error,
  path/query encoding, AbortSignal. 204 не читает body; пустой202 ->undefined.
- `src/api/errors.ts`: ApiError c authoritative HTTP status/code/traceId/fieldErrors;
  network/aborted/timeout/invalid_response/invalid_request — отдельные ClientError kinds.
  HTTP400/401/403/404/409/429/5xx сохраняют статус даже при не-JSON/сломанных error bodies.
  Сырые детали/URL/Response/Request/fetch cause/неизвестные metadata не сохраняются.
- CSRF GET получает настоящий `{token}`, хранит его только в закрытой памяти и
  автоматически добавляет X-CSRF-TOKEN ко всем unsafe admin calls, включая login/logout.
  prepareCsrf возвращает void; сам CSRF route исключён из публичного typed request API.
  Concurrent acquisition объединяется; отмена одного caller не ломает другого,
  отмена последнего прерывает общий fetch. Late result после invalidation отклоняется.
  После успешного login/logout, HTTP401 или csrf_invalid кеш CSRF очищается;
  ни команда, ни получение CSRF в рамках этой команды автоматически не повторяются.
- `src/api/queryKeys.ts`: auth.me/dashboard/users/detail/sessions/audit factories,
  все server-side параметры в ключах, стабильные resource prefixes для invalidation.
- `src/app/queryClient.ts`, AppProviders.tsx, main.tsx: fresh QueryClient на root mount,
  retry:false для queries/mutations, networkMode:always для mutations без offline queue,
  gcTime0 для неактивных mutations. В тестах fresh clients, никакого global query cache.
- HTTP tests, Query/cache/provider tests и расширение существующего PostgreSQL/API
  AdminReadTests для двух schema refs. Из backend production code — только два attributes
  в AdminUsersController. CQRS/DI/Domain/Infrastructure/security policies не менялись.
- README/frontend README/CHANGELOG/.env.example обновлены, scripts/*.mjs включены в ESLint.
  Пустой api/.gitkeep удалён. Новых миграций, DB/config secret changes, endpoint routes нет.

## 3. Решения

Типы не дублируются: OpenAPI сохраняет number|string, nullable/optional properties,
строковые role/status и реальный casing query parameters. Ошибки с динамическими
ProblemDetails extensions проверяются runtime по документированному transport contract.
Полная runtime DTO schema validation не вводилась; JSON success доверяет backend schema,
на границе ровно один assertion результата; CSRF shape проверяется отдельно.

openapi-typescript7.13 требует TypeScript^5.x. Первая установка с TS6 закономерно дала
ERESOLVE без изменений dependency tree. Закреплён TS**5.9.3**, strict/noUncheckedIndexedAccess
и ESLint type-aware checks сохранены. --force/--legacy-peer-deps не использовались.

Отмена/таймаут принадлежат вызывающей стороне: signal, AbortController,
AbortSignal.timeout/any. Нет скрытого deadline/retry. При отмене отправленной команды
сервер мог уже выполнить её; требуется обновить read data, а не автоматически replay.
CSRF/password/one-time tokens нельзя помещать в Query keys/data/mutation variables;
будущие sensitive calls используют клиент напрямую и кешируют лишь безопасный результат.

Визуальное представление bootstrap не менялось. UI-тема/навигация/App Shell — 11.3,
auth lifecycle/route guards/cache clearing/navigation при401/Forbidden UI — 11.4.
В этом этапе клиент лишь различает статусы, не делает redirect или скрытый login.
Стартовое приложение не вызывает API; feature hooks/бизнес-экраны не добавлены.

## 4. Проверки

### Commands run

Из frontend:

```powershell
npm ci
npm run format
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run test -- --run
npm run build
$env:IDENTITY_OPENAPI_URL = 'http://127.0.0.1:5085/openapi/v1.json'
npm run api:generate
npm run api:check
$env:IDENTITY_API_PROXY_TARGET = 'http://127.0.0.1:5085'
npm run dev
```

Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage11-2'
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/Identity.Api --no-build --configuration Release --no-launch-profile --urls http://127.0.0.1:5085
node TestResults/stage11-2-browser-smoke.mjs
pwsh -NoProfile -File scripts/Test-Stage7Smoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

Дополнительно: реальный OpenAPI и ProblemDetails прочитаны Invoke-RestMethod;
negative generator run с IDENTITY_OPENAPI_URL=/health/live ->expected exit1,
SHA256 schema.d.ts до/после совпал; затем api:check с реальным контрактом exit0.
Generated check повторён после добавления generated-file header.

### Observed results

- npm ci exit0:333 packages, audit334, известных уязвимостей не обнаружено.
- typecheck/lint/format:check/build exit0, frontend build без warnings.
  **48 frontend tests passed, 0 failed/skipped**, 4 files. Первый test:run —44 passed;
  после четырёх дополнительных cancellation/DELETE cases итоговый test -- --run —48.
  Compile-time negative tests проверяют неизвестный route/method/body, отсутствующий
  path parameter, запрет native login и прямого CSRF request.
- GET/POST/DELETE, credentials/headers, CSRF reuse/login/logout/reset/401/invalidCSRF,
  пустые202/204, public flows без admin CSRF, HTTP status matrix, network error,
  cancellation/timeout/concurrent cancellation/late invalidation, no retry/storage/logs,
  query keys/prefixes/cache isolation/offline mutation/provider remount проверены.
- Browser Chromium: real Vite ->actual backend401 unauthorized и503 admin_unavailable
  с traceId, ровно2 requests/без retries; bootstrap не выполняет API calls.
  360/768/1280px, без overflow/page errors, storage пуст. Это transport/bootstrap smoke,
  не authenticated login E2E. Script в ignored TestResults, production mocks нет.
- .NET restore/build/format exit0; **0 warnings/0 errors**.
  **470 backend tests passed, 0 failed/skipped**: Unit147, API45, Integration278.
  Реальные PostgreSQL/Mailpit, включая concurrency/lifecycle/migrations и OpenAPI regression.
- EF model check exit0, pending model changes нет; idempotent migration.sql создан.
- Docker compatibility smoke exit0: новый non-root API image healthy/version1.0.0,
  migrations/register/Mailpit/verify/login/refresh/restart/replay/me/logout/sessions/
  password reset/change/deactivation/headers. Только прежние совместимые сценарии;
  frontend/admin deployment не заявляется. Свой Compose project
  identity-stage7-smoke-7e1885ccc7d44931ae3a5eaad73c4884 очищен.
- Secret guard self-tests + current tracked/untracked tree и diff --check проходят.

Промежуточные ошибки исправлены: ERESOLVE TS6/openapi-typescript ->совместимый TS5.9;
TS erasableSyntaxOnly запретил parameter properties новых Error classes ->обычные fields;
typed lint и позиция ts-expect-error уточнены. Итоговые команды прошли.
Блокер отсутствующих200 в OpenAPI устранён только после отдельного разрешения человека.

Ignored artifacts: TestResults/stage11-2-docker-smoke.log, stage11-2-browser-smoke.mjs,
TRX stage11-2 в тестовых проектах, migration.sql/dist/node_modules. Никаких deploy,
registry publication, commit/tag, production accounts или внешних messages.

## 5. Безопасность

CSRF request token в closure, не Query cache/storage/cookie JS; credentials отправляются
только same-origin и в JSON, redirect заблокирован. Unsafe команды не replayятся после
network/401/CSRF, mutation offline queue отключена. Нет логирования request/body/token,
raw transport exception details не сохраняются. URL source env и generated types —
tooling/type-only; никакой backend configuration/private keys в browser bundle.
Все прежние backend HTTPS/CSRF/cookie/role/session checks сохранены и прошли регрессию.

## 6. Ограничения / Known issues

Блокеров11.2 нет. Cookie-auth/login/logout UI, access guards/401 redirect/cache clearing,
Forbidden screen и feature queries — последующие этапы. Наличие клиента само по себе
не предоставляет доступ к admin данным. Настройка HTTPS сертификатов/production VPN/MFA/
SMTP/load/CSP, frontend Docker/CI/proxy deployment и полноценные user E2E — не проверялись
и не реализованы; stage12 не начат. Полная runtime DTO validation не входит в этот этап.
OpenAPI стандартная ProblemDetails schema не перечисляет runtime extensions; адаптер
читаёт только документированные code/traceId/errors с проверкой формы.

Текущие REST API и архитектура совместимы. Согласованное исключение — только OpenAPI
metadata + regression test. Query/HTTP инфраструктура не меняет UI и не вызывает API сама.

## 7. Следующий этап

**Next: 11.3 — UI-тема, навигация и адаптивный App Shell.**
Не начат; продолжение только по отдельному запросу пользователя.

---

# Исторический отчёт: этап 11.1

**Completed: 11.1**

**Next: 11.2 — только по отдельному запросу пользователя.**

## 1. Этап

**11.1 — Bootstrap проекта и инструменты разработки**, 2026-10-19.
Источник требований: AGENTS.md, Identity_Codex_Spec.md и
Identity_Admin_Codex_Addendum_v1.2.md (§2, §8/11.1, §9).
Изучены существующие Controllers/DTO, конфигурация запуска, документы API и Git.
На старте дерево чистое. Backend 0–10 завершён: stage10 — **c87306d**,
stage9 — **9cff58b**, stage8 — **0b8536a**; v1.2 addendum — **c1ebb15**.

## 2. Реализовано

- Новый самостоятельный npm-проект `src/Identity.Admin.Web`, private package,
  без .NET project references и изменений solution/backend.
- React/React DOM 19.3.1, Ant Design 6.6.6, icons 6.3.4, Vite 8.3.4.
  React Router 7.18.4 и TanStack Query 5.104.4 установлены согласно стеку,
  но providers/routing/API integration не реализованы раньше своих подэтапов.
- TypeScript 6.0.3: strict/noUncheckedIndexedAccess, app/tests и Node config
  проверяются отдельными tsconfig references. Alias `@/` согласован с Vite/Vitest.
- ESLint 10 flat config, type-aware TypeScript rules, React hooks/Fast Refresh,
  Prettier, Vitest 5 + RTL + jsdom 29; Playwright установлен для будущих сценариев.
  Версии зависимостей exact, `package-lock.json` включён; `npm ci` проверен.
- Минимальные каталоги `src/{app,api,components,features,pages,test}`, `public`;
  пустые будущие каталоги сохранены через .gitkeep. Никаких фиктивных реализаций.
- `src/main.tsx`: React StrictMode/createRoot, Ant Design reset и базовые стили.
  `src/app/App.tsx`: настоящий рендер `Identity Console` с Ant Design Typography,
  semantic main/h1; один RTL smoke-test, автоматическая очистка DOM в test/setup.
- Скрипты dev/build/preview/typecheck/lint/format/format:check/test/test:run.
- `vite.config.ts`: только loopback IPv4 `127.0.0.1:5173`, strictPort,
  dev proxy `/api` без rewrite к текущему local API `http://localhost:5080`.
  Target и optional HTTPS cert/key paths задаются server-only env; проверка TLS
  backend не отключена. `envPrefix: []` исключает env из browser bundle.
- `.env.example`, frontend `.gitignore`/Prettier config, frontend README с
  requirements, запуском, командами, alias/feature boundaries, proxy/HTTPS/security.
  Root README/CHANGELOG и scoped EditorConfig indent2 обновлены.

Ключевые файлы: `src/Identity.Admin.Web/{package.json,package-lock.json,
vite.config.ts,eslint.config.js,tsconfig*.json,index.html,README.md,.env.example}`,
`src/{main.tsx,app/App.tsx,app/App.test.tsx,app/styles.css,test/setup.ts}` внутри frontend.
**API, CQRS, DI, БД и миграции не менялись.** Product release 1.0.0 не повышен;
0.0.0 — внутренняя версия нового private npm package, не отдельный опубликованный релиз.

## 3. Решения

Только bootstrap, без API-клиента, auth/session context, query-key factories,
protected routes, Dashboard, sidebar, бизнес-форм или публичного token flow.
Тема Ant Design пока стандартная: centralized tokens и App Shell относятся к 11.3.
Компоненты/абстракции будущих сценариев не созданы без необходимости.

Node 22.16.0/npm 10.9.2 доступны в окружении. jsdom 30 требует более новый Node,
поэтому выбран совместимый jsdom 29.1.1; TypeScript 6 совместим с typescript-eslint 8.
Engine проекта — Node ^22.13 || ^24 || >=26; исходный backend toolchain сохранён.
Проверены официальные руководства Vite/Ant Design/Vitest и npm engine/peer metadata.

HTTP подходит для bootstrap. Admin cookie flow в дальнейшем требует HTTPS на
браузерном origin и backend HTTPS либо явной доверенной proxy-конфигурации.
Предусмотрены optional trusted PEM cert/key paths; private key не публикуется Vite.
Нет CORS bypass, `secure:false`, отключения CSRF или изменения backend policy.

## 4. Проверки

### Commands run

Из `src/Identity.Admin.Web`:

```powershell
npm ci
npm run typecheck
npm run lint
npm run format
npm run format:check
npm run test -- --run
npm run test:run
npm run build
$env:IDENTITY_API_PROXY_TARGET = 'http://localhost:5085'
npm run dev
npm exec playwright install chromium
```

Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage11-1'
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/Identity.Api --no-build --configuration Release --no-launch-profile --urls http://localhost:5085
node TestResults/stage11-1-browser-smoke.mjs
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

Дополнительно Invoke-WebRequest/Invoke-RestMethod: Vite HTML, реальный
`http://localhost:5085/openapi/v1.json`, direct/proxied `/api/v1/admin/auth/csrf`.
`rg` проверил dist на proxy target/certificate env names, local backend URLs,
Database connection marker и private-key marker: совпадений нет.

### Observed results

- npm ci exit0: 309 packages, audit310, известных уязвимостей не обнаружено.
- typecheck/lint/format:check/test/build exit0; один smoke-test passed, без skips.
  `test -- --run` и `test:run` проверены отдельно. Production dist создан:
  JS452.99kB (gzip148.78kB), CSS3.06kB; предупреждений build нет.
- .NET restore/build/format exit0; **0 warnings / 0 errors**.
  **470 backend tests passed, 0 failed, 0 skipped**:
  Unit147 + API45 + Integration278, реальные PostgreSQL/Mailpit в Docker.
- EF model check exit0: pending model changes отсутствуют; новых миграций нет.
  Up/Down/reapply/constraints проверены существующим integration suite.
- Live OpenAPI3.1.1 доступен, содержит16 admin paths; контракт не подменён.
  Vite HTML200, proxy сохраняет API JSON Content-Type и503 от выключенного
  AdminAccess, не заменяет его SPA HTML. Это транспортный smoke, не auth E2E.
- Ad hoc Playwright Chromium smoke: 360/768/1280px, main/h1 видны,
  горизонтального переполнения/JS page errors нет, browser storage пуст.
  Скриншот просмотрен. Script/screenshot в ignored TestResults; постоянная E2E suite
  будущих пользовательских сценариев не добавлена раньше соответствующего этапа.
- Secret guard self-tests + tracked/untracked tree, diff --check проходят.
  Временные API/Vite остановлены, тестовые containers удалены fixture cleanup.

Первый npm metadata request ограничен offline-cache sandbox; повтор разрешён.
Первый dotnet restore не запустился в sandbox; повтор разрешён и успешен.
Первый Vitest — spawn EPERM из sandbox; с разрешёнными child processes проходит.
Первый Chromium launch не нашёл browser binary; после `playwright install chromium`
smoke прошёл. Первый временный API port53680 запрещён ОС; порт5085 работает.
Vite default localhost привязался к IPv6, недоступному HTTP-клиенту этого окружения;
явный IPv4 loopback исправил запуск/HTTP-проверки. Финальные проверки выполнены
после изменения config. Эти промежуточные сбои не являются прошедшими проверками.

## 5. Безопасность

Frontend не получает backend secrets, не обращается к БД/SMTP, не делает fetch,
не хранит auth/one-time tokens или пароли и не показывает моковые бизнес-данные.
Server-only env не включены в bundle; `public/` предназначен только для публичных
ресурсов. Env/local/dependency/build/browser-test artifacts исключены из Git.
Dev server доступен только на loopback; никакого внешнего deployment не было.
Backend HTTPS/cookie/CSRF/role/session validation не менялись и прошли регрессию.

## 6. Ограничения / Known issues

Блокеров 11.1 нет. HTTPS с пользовательскими cert files и production TLS не проверялись;
cookie-auth, CSRF API-клиент и бизнес-сценарии frontend ещё не реализованы по scope.
`preview` не имеет dev API proxy и не является production сервером.
Frontend Dockerfile/Compose/reverse proxy/CSP/CI — этап12, не добавлены; отдельный
Docker image build/smoke не повторялся, существующие backend images/config не менялись.
Docker использован всеми реальными integration tests; удалённый CI/production
VPN/SMTP/load/axe/Lighthouse не запускались. React Query/Router пока только dependencies.

Новых backend требований или несовместимостей API, блокирующих bootstrap, не выявлено.
Рабочие изменения не закоммичены; tag/publish/deploy не выполнялись.

## 7. Следующий этап

**Next: 11.2 — типизированный HTTP-клиент, OpenAPI, CSRF.**
Не начат. Реализация только после отдельного запроса пользователя.

---

# Исторический отчёт: этап 10 (до текущего frontend-подэтапа)

## 1. Этап

**10 — Administrative write commands**, завершён 2026-10-19.
Изучены AGENTS.md, Identity_Codex_Spec.md, Identity_Admin_Codex_Addendum.md целиком,
текущий код, Git history и предыдущий IMPLEMENTATION_STATUS.md. Дерево было чистым;
этап9 подтверждён commit **9cff58b**, этап8 — 0b8536a, этап7 — c5f230d.
Выполнен только этап10 дополнения (§3–5, §8). Этапы11/12 не начинались.
Новый frontend, страницы активации/reset и внешнее развёртывание не выполнялись.

## 2. Реализовано

### HTTP и Application

Все административные команды — MVC Controllers + собственный CQRS, cookie-политика
IdentityAdmin (только полный администратор), HTTPS, CSRF и существующие IP/account limits.
Actor/user-session IDs берутся из cookie. Приложение не принимает их от клиента в JSON.

| Метод | Endpoint | Ответ |
|---|---|---|
| POST | /api/v1/admin/users/invitations | 202, пустое тело |
| POST | /api/v1/admin/users/{userId}/invitations/resend | 202, пустое тело |
| POST | /api/v1/admin/users/{userId}/password-reset | 202, пустое тело |
| POST | /api/v1/admin/users/{userId}/disable | reason required, 204 |
| POST | /api/v1/admin/users/{userId}/enable | 204 |
| DELETE | /api/v1/admin/users/{userId}/sessions/{sessionId} | 204 |
| POST | /api/v1/admin/users/{userId}/sessions/revoke-all | 204 |
| POST | /api/v1/auth/invitations/accept | public token/newPassword JSON, 204 |

- Отдельные command/handler/validator slices: InviteUser, ResendInvitation,
  RequestAdminPasswordReset, DisableUser, EnableUser, RevokeUserSession,
  RevokeAllUserSessions, AcceptInvitation. Явная регистрация DI, TimeProvider,
  CancellationToken, явные транзакции. Контроллеры не получают DbContext/repositories.
- AdminWriteAuthorization повторно проверяет actor Active+verified, текущую роль и
  session owner/revocation/idle/absolute expiry внутри mutation transaction. Проверка
  middleware не является единственным разрешением на изменение.
- Invite: Invited без credentials + hash-only Invitation token + AES-GCM encrypted
  whole-email outbox + audit actor/target атомарно. Пароль администратором не назначается.
  Любой существующий email ->409 admin_email_conflict; конкурентная unique violation
  также переводится в409 без частичных записей. Public register сохраняет generic202.
- Resend только Invited без credentials; прежние invitation tokens инвалидируются,
  новый hash/token/outbox/audit создаются в той же транзакции. SMTP-доставка at-least-once:
  старые queued emails могут прийти, но их tokens уже непригодны.
- Public accept: purpose/ownership/expiry/single-use/status/отсутствие credential,
  user -> token locks, начальный PBKDF2 credential и verified Active атомарно; другие
  invitations инвалидируются. Пароль выбирает получатель, прежняя NFKC/password policy.
  Автоматический login и grant admin-role не выполняются. Повторный/просроченный/неподходящий
  token/state/credential -> одинаковый400 invalid_invitation_token.
- Admin reset переиспользует30-minute token/encrypted email/public reset API. Только
  Active+verified с credentials (временный brute-force lock допустим). Запрос не меняет
  пароль/сессии; успешное применение reset отзывает оба типа сессий существующим lifecycle.
- Disable сохраняет исходный Active/PendingVerification/Invited, actor/reason/time,
  отзывает native/BFF refresh families и admin cookies, инвалидирует verification/reset/
  invitation tokens. Enable восстанавливает исходный status, не подтверждает email,
  не создаёт credential и не оживляет старые tokens/sessions. Deactivated не реанимируется.
- Повторный disable/enable в уже целевом допустимом состоянии ->идемпотентный204;
  original disable reason/audit не переписывается. Enable не сбрасывает отдельный
  brute-force lockout. Для legacy Disabled без prior status: Active только при verified,
  иначе PendingVerification. При enable disable metadata очищается.
- Последний Active+verified полный администратор защищён от disable, CLI revoke/downgrade
  и self-deactivate общей advisory transaction lock. User locks берутся по sorted IDs
  перед session/token locks. ReadOnly не считается резервным полным администратором.
- Targeted revoke работает с существующей native/BFF sessions таблицей из stage9,
  проверяет owner; отсутствующая/чужая session404, повторный revoke204. Revoke-all
  отзывает native/BFF и admin sessions, включая текущую при self-target. История tokens
  сохранена для replay detection; новые токены при revoke не выдаются.
- Audit actor/target/time/correlation/whitelisted action сохраняется с commit. Добавлены
  invitation_accepted и admin_revoked_all_sessions; используются существующие имена
  admin_invited_user/admin_resent_invitation/admin_requested_password_reset/
  admin_disabled_user/admin_enabled_user/admin_revoked_session. Free-text reason в audit
  metadata не помещается, хранится на user во время блокировки. Секретов/ссылок нет.

### Infrastructure / Domain / конфигурация

- InvitationRepository: purpose-specific owner lookup/row lock, атомарная invalidation,
  включая future-issued links без нарушения timestamp constraints. InvitationIssuer
  повторно используется invite/resend; handlers не вызывают друг друга.
- Outbox worker поддерживает новый тип invitation; сохраняет encrypted payload при
  SMTP failure/retry, очищает после успеха. Key ring/startup validation/maintenance
  учитывают Invitations, даже когда registration/reset/JWT отключены.
- Invitations.Enabled=false по умолчанию, LifetimeSeconds=259200 (72h), допустимо60–259200.
  Email.InvitationUrlTemplate — фиксированный URL только с #token={token}. Production:
  outbox worker, SMTP TLS, корректные отдельные AES keys и non-dev key IDs обязательны.
  Admin password reset при AdminAccess+PasswordReset требует fragment-only reset URL.
- appsettings/.env.example/compose/production.env.example обновлены. Dev Compose invitation
  flag независим от AdminAccess/TLS/key-ring setup; существующий plain HTTP dev bootstrap
  не превращён в небезопасную cookie-админку. Никакие реальные env/keys пользователя не менялись.
- Новая additive migration **20261019024608_AdminWriteLifecycle**: nullable
  users.status_before_disable + check; revoke reasons AccountDisabled=7,
  AdministratorRevoked=8; audit whitelist. Старые enum values/rows/FK/indexes сохранены,
  prior state для legacy rows не выдумывается. Startup migrations не добавлены.
- docs/admin-write-api.md: все contracts/curl, настройки, idempotence/conflicts,
  transaction/locking decisions, SMTP semantics, JWT lag, release boundaries.
  README/CHANGELOG, admin-access/read/api/architecture/security/deployment/production docs
  обновлены; smoke env фиксирует Invitations=false для прежних compatibility сценариев.

Ключевые файлы относительно корня:

- Domain/Entities/{User,SecurityAuditEvent}, Enums/{SessionRevokeReason,SecurityEventType}.
- Application/Features/Admin/Users/{InviteUser,ResendInvitation,RequestPasswordReset,
  DisableUser,EnableUser,RevokeUserSession,RevokeAllUserSessions}; Features/Auth/AcceptInvitation;
  Features/Admin/Common/{AdminWriteAuthorization,AdminWriteValidation,InvitationIssuer};
  Abstractions/{Security/IInvitationSettings,Notifications/IInvitationEmailFactory,
  Persistence/IInvitationRepository}, Common/AdminWriteExceptions, DependencyInjection.
- Infrastructure/Email/{InvitationOptions,InvitationEmailFactory,EmailOptions,
  EmailDependencyInjection,EmailOutboxProcessor}, Security/OutboxKeyRing,
  Maintenance/SecurityMaintenanceWorker, Persistence/Repositories/InvitationRepository,
  entity configurations, Migrations/AdminWriteLifecycle + Designer/snapshot.
- Api/Controllers/Admin/AdminUserCommandsController, AuthController,
  Contracts/AdminUserRequests, Middleware/ApiExceptionHandler, appsettings.json.
- tests/Identity.IntegrationTests/AdminWriteTests, обновлённые AdminAccessTests/
  AdminReadTests/PersistenceTests; UnitTests/AdminWriteValidationTests и
  RegistrationSecurityTests (новый options constructor argument).
- docs/admin-write-api.md и перечисленные docs/config samples,
  scripts/Test-Stage3Smoke.ps1, compose.yaml.

## 3. Решения

Clean Architecture/Controllers/custom CQRS сохранены. Domain standalone; Application ->
Domain; Infrastructure -> Application+Domain; API -> Application+Infrastructure. Новых
NuGet libraries, Identity user management, mediator, generic repository или внешней
business-зависимости нет. Существующие REST login/refresh/register/profile APIs не ломались.
Версия release1.0.0 сохранена, дополнение v1.1 описано как Unreleased.

Одна глобальная administrators lock для административных writes — сознательный простой
MVP-компромисс: корректность actor/target/last-admin transitions вместе с CLI важнее
максимального throughput административных операций. Публичные user lifecycle операции
продолжают использовать user-first row locks. Нет locks вокруг SMTP: письмо лишь queued
транзакционно, network IO выполняет существующий outbox worker после commit.

Для точного enable добавлено поле prior status, а не предположение Active для всех.
Deactivated ->conflict; duplicate invitation ->private admin409; existing disabled/active
state operations идемпотентны. Targeted session endpoint соответствует native/BFF DTO
этапа9, bulk revoke включает admin transport. Решения/контракты явно описаны в документации.

Invitation feature flag отдельный: admin read/login deployments не требуют внезапно SMTP
и новых templates. Production templates строже для admin reset, как требует addendum.
SPA activation/reset pages не подменены server-side заглушками: это этап11. В этапе10
Mailpit ->API acceptance выполняется доверенным клиентом. HTTP202 не обещает доставку.

## 4. Проверки

Выполнены из корня, .NET SDK10.0.402, PowerShell7.6.5, Docker Linux:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations add AdminWriteLifecycle --project src/Identity.Infrastructure --configuration Release --no-build
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AdminWriteTests|FullyQualifiedName~AdminWriteValidationTests|FullyQualifiedName~PersistenceTests' --logger 'trx;LogFilePrefix=stage10-targeted'
dotnet test Identity.slnx --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AdminWriteTests|FullyQualifiedName~AdminWriteValidationTests|FullyQualifiedName~PersistenceTests' --logger 'trx;LogFilePrefix=stage10-targeted-fixed'
dotnet format Identity.slnx --no-restore
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage10-full'
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/Identity.Infrastructure --configuration Release --no-build --output migration.sql
dotnet list Identity.slnx package --vulnerable --include-transitive
pwsh -NoProfile -File scripts/Test-Stage7Smoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

- Итоговые restore/build/format/EF checks exit0. Build: **0 warnings / 0 errors**.
- **470 passed, 0 failed, 0 skipped**: Unit **147**, API **45**, Integration **278**.
  Это прежние410 +60 новых:14 unit validation/domain/template cases и46 PostgreSQL/API/
  Mailpit/transaction/concurrency cases. EF InMemory/SQLite не использовались.
- Проверены Mailpit invitation ->public accept ->login/me, hash-only72h token, отсутствие
  credentials до acceptance и отсутствие роли после; secret-free response/audit/logging;
  invalidated/resend/expiry/reuse, неправильный purpose и существующий credential;
  duplicate invite race, принудительно синхронизированная регистрация между lookup/save
  (admin409/registration202 без partial data), concurrent acceptance/resend;
  сохранение Active/Pending/Invited при disable/enable; blocked login/refresh/Identity JWT
  и admin cookie; last-admin concurrent self-disable; disable vs refresh; targeted/bulk
  owner/revocation semantics; Mailpit admin reset ->single use ->all-session revoke;
  все7 writes ReadOnly403 и missing CSRF400; recheck actor role/session/status/expiry
  внутри handler даже после старого cookie authorization; все8 use cases rollback.
- Rollback сравнивает безопасный SHA256 полного DB snapshot (включая admin revocation,
  исключая отдельный auth last_seen touch); raw hashes/ciphertext не печатаются assertion.
  Invitation-only host (без registration/JWT/reset) загружает ключи; SMTP failure
  сохраняет encrypted payload и machine error. Invalid lifetime/URL config/disabled flag
  проверены. Недопустимые startup options могут агрегироваться .NET StartupValidator.
- Первый targeted run: worker отклонил новый invitation type, Mailpit получил0 писем.
  Исправлен whitelist EmailOutboxProcessor; Mailpit delivery/acceptance в полном run зелёные.
  Второй expanded run выявил слишком узкое ожидание теста: AggregateException с двумя
  OptionsValidationException вместо одиночной TTL ошибки. Assertion уточнён, отказ startup
  не ослаблен. Итоговый полный run прошёл; промежуточные неуспешные проверки не скрыты.
- Все6 migrations Up/Down/reapply, constraints и legacy data-preservation tests прошли
  на PostgreSQL17. EF pending model changes нет; idempotent migration.sql сформирован.
- NuGet audit8 проектов: известных уязвимых packages по текущим sources не обнаружено.
- Docker compatibility smoke exit0: build нового non-root API image, применение всех6
  migrations, register ->Mailpit ->verify ->login ->refresh ->me ->logout ->failed refresh,
  плюс replay/restart/sessions/password/reset/deactivation и headers/errors. Docker health
  healthy. Admin/invitations выключены в compatibility smoke; новые admin/invitation API
  отдельно проверены WebApplicationFactory + реальный PostgreSQL/Mailpit. Это не browser
  frontend E2E. Временные containers/network/volume/env/keys полностью удалены.
- Secret guard self-tests/current tracked+untracked tree и git diff --check проходят.
  Frontend lint/test неприменимы до этапа11. Remote GitHub CI, production TLS/VPN/SMTP,
  MFA и внешний deployment не запускались и не заявляются passed.

Ignored artifacts: TestResults/stage10-targeted.log, stage10-targeted-fixed.log,
stage10-full-tests.log, stage10-docker-smoke.log, stage10-nuget-audit.log;
TRX stage10-full в TestResults тестовых проектов. Секреты/generated SQL/logs/TRX
не добавлены в Git. Commit/tag/registry publication не выполнялись.

## 5. Безопасность

Нет выдачи пароля/token/hash администратору, нет установки произвольного пароля через
admin API. Invitation подтверждает email через одноразовый случайный token, хранит только
SHA256, письмо — AES256GCM outbox; production SMTP требует TLS. URL token только fragment
для новых invitation/admin reset flows. Никаких user-controlled redirects.

Backend denies ReadOnly, requires cookie+CSRF и повторно проверяет полномочия в транзакции.
Общая блокировка защищает последнего администратора и конкурентные actor/target операции.
Disable отзывает обе session transports/refresh chains и старые одноразовые ссылки;
enable не является email verification, recovery token или session resurrection.
Audit/metrics содержат whitelisted event types/IDs/time/correlation без passwords/tokens/
links/reason text. Существующая crypto и key storage архитектура повторно использована.

## 6. Ограничения / блокеры

Блокеров этапа10 нет. Frontend и страницы /account/activate/reset-password — этап11,
production integrated UI/E2E/hardening/release — этап12, не реализованы. Invitations opt-in,
готовность фиксированных web templates и VPN/trusted ingress/DP/AES/SMTP secrets задаёт
оператор. До MFA admin не публикуется свободно в интернет. Старые emails могут доставляться
после resend/block, но их tokens недействительны; SMTP остаётся at-least-once.

Targeted revoke относится к native/BFF family из stage9; revoke-all дополнительно включает
admin sessions. Автономный JWT в стороннем API может жить до exp (+skew); глобального revoke
обещания нет. Rate limits локальные/общие с текущими AuthRateLimits; все admin writes
сериализованы global lock (MVP throughput tradeoff). Session/audit retention не менялась;
admin session purge остаётся прежним ограничением. Уже completed requests не отменяются.

Downgrade после новых events/revoke reasons требует data compatibility plan; миграция не
удаляет несовместимые rows молча. Legacy Disabled без prior status восстанавливает verified
Active или unverified Pending, никогда не подтверждает email автоматически. Фактические
production TLS/SMTP/VPN/load/backup rehearsal и удалённый CI не выполнялись.

Противоречий требованиям этапа10 не выявлено; не детализированные idempotence/legacy state/
transport choices явно зафиксированы в docs/admin-write-api.md. Внешние продукты, tenants,
бизнес-роли или зависимости не добавлены. Следующие этапы не реализовывались заранее.

## 7. Следующий этап

Рекомендация: **11 — React Admin Web** (React/TS/Vite/Ant Design, generated API types,
secure centralized cookie/CSRF client, admin/public account pages, component/Playwright
checks). Не начат; только после отдельного запроса пользователя.
