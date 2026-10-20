# Текущий статус: этап 11.14 — сквозная доработка frontend и приёмка

**Completed: 11.14**

**Next: 12 — только по отдельному запросу пользователя.**

## 1. Этап

11.14 из Identity_Admin_Codex_Addendum_v1.2.md, 20–21.10.2026. Прочитаны AGENTS.md,
base Spec, дополнение, статус и текущие API/UI/tests. Начальное дерево чистое,
HEAD **14bc31b**. Сохранены backend0–10, frontend11.1–11.13 и demo launcher.
Этап12, backend/API/schema/migrations/MFA/proxy/deployment не изменялись.

## 2. Реализовано

- UX review основных маршрутов/диалогов при360/768/1280, keyboard-only сценарий,
  доступность полей/кнопок и подтверждений. PageTitle даёт heading focus и document
  title при mount/result; filter/refetch не крадёт фокус. PublicLayout получил skip link.
  Drawer navigation переводит фокус в main, отмена возвращает trigger; Disable/Enable
  после ответа сервера переводит фокус на новый action, заменивший старый.
- При визуальной проверке исправлено обрезание длинных Card headings на mobile:
  единый ConfigProvider card.styles.title.whiteSpace=normal. Graph wrapper получил
  role=group для своего aria-label; сохранены Recharts keyboard layer и values table.
  Accessible name Refresh account совпадает с видимой подписью.
- Общий CommandError для4 групп admin writes: inline business conflicts, CSRF,
  transport/invalid-response/5xx uncertainty с конкретным recovery. Успешные Ant
  message toasts остаются единственными и только после HTTP success;202 означает
  принятие в обработку. Нет automatic unsafe retry/дублирующих global error toasts.
  Auth/public password flows сохранили отдельную безопасную presentation и не
  передают пароли/one-time tokens в Query.
- Invitation success теперь инвалидирует audit вместе с users/dashboard;401 от
  refetch /auth/me очищает старый profile и protected query/mutation data даже при
  retained successful Query data. Regression assertions для обеих проблем.
- app/theme.ts: AA text contrast через централизованные Ant Design tokens:
  official blue7/8/9, читаемые description/placeholder/semantic text и scoped
  Tag/Button status colors. Это явное уточнение exact-default color baseline11.9
  ради требования11.14 contrast; стандартные layout/spacing/radii/shadows сохранены,
  CSS color overrides нет. PasswordInput общий для3 форм: standard large input
  и24px visibility target; login email также large для согласованности.
- ESLint guards против случайного fetch вне central client, console/storage/test
  fixture imports в production. Существующие4 command hooks не переписывались
  в универсальную абстракцию. Контракты REST, cookie/CSRF и backend policies прежние.
- Dev-only @axe-core/playwright **4.14.0** (registry verified, exact pin + lockfile,
  2 added packages). Runtime dependencies не менялись.
- e2e/acceptance.spec.ts:12 новых real-backend scenarios:40 axe page/dialog audits,
  ReadOnly/no writes, реальные CSRF400 и manual retry, все anonymous admin routes,
  in-place /me401, focus после обоих status transitions и keyboard-only journey.
  Project после public account, перед visual; default полный suite с retries0.
- e2e/fixture.ts применяется ко всем specs и отдельному anonymous account context:
  known secrets не должны попасть в console/request/page URLs, storage пуст,
  cookie flags проверяются через context/browser. Screenshot guard запрещает
  заполненные password fields (включая revealed text) и credential-bearing URLs;
  cleanup убирает значения перед failure snapshots. Traces/video/automatic screenshots
  выключены, реальные Mailpit tokens только Node memory. Sign-in input failures
  sanitized; harness подавляет строки с известными fixture passwords перед выводом.
- scripts/Test-AdminWebSmoke.ps1: optional -AcceptanceOnly для isolated UX/axe checks,
  без изменения default full suite; новая БД/почта/env/ключи и cleanup на каждый run.
- README frontend/root, CHANGELOG, docs/frontend-acceptance.md и исторический
  Ant Design baseline обновлены; добавлены3 проверенных safe screenshots.
  Матрица сценариев, env/dev HTTPS proxy, команды и navigation examples задокументированы.

## 3. Решения / API / конфигурация / миграции

Новых API/DTO/backend/migrations/runtime config flags нет. Production release,
Docker frontend/proxy/CI/CSP hardening остаются12. Dev-only axe нужен для реальных
contrast/aria/target checks, не попадает в production bundle. Общая текстовая
политика использует props/constants, en-US и Intl/local-time helpers; полноценная
локализация не вводилась. Цветовые уточнения и40px password controls — стандартные
theme tokens/large variant, а24px icon layout — собственный geometry class.

Чтение использует ErrorState и явный GET retry; admin write errors inline в modal,
server-confirmed success один раз через App.message. Password/account completion
inline без autologin. Семантика конфликтов/ошибок определяется существующим API.

## 4. Проверки

### Commands run

Frontend:

```powershell
npm view @axe-core/playwright version
npm install --save-dev --save-exact @axe-core/playwright@4.14.0 --no-audit --no-fund
npx prettier --write src/app/AppProviders.tsx src/features/dashboard/dashboard.test.tsx e2e/acceptance.spec.ts e2e/antDesign.spec.ts playwright.config.ts
npx prettier --write src/features/users/userDetails.test.tsx
npx prettier --write README.md docs/frontend-acceptance.md docs/ant-design-alignment.md
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run api:check
npm run test:e2e
```

Prettier фактически запускался на затронутых отдельных файлах/каталогах; без изменений
unrelated sources. api:check/test:e2e выполняет harness, test:e2e включает npm run build.
Repository root:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage-11-14'
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1 -AcceptanceOnly
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- Final TypeScript/ESLint/Prettier exit0; **330 Vitest tests passed**,19files,
  0failed/skipped. Это324 прежних+5 common-error cases+1 session-refetch regression.
  Invite audit invalidation и chart group проверены дополнительными assertions
  существующих cases. Последние полные runs330passed19.81s/19.03s/19.00s.
- .NET restore/build exit0,0warnings/0errors. **470 tests passed**,147Unit/45API/
  278Integration,0failed/skipped, real PostgreSQL/Mailpit; integration1m27s.
  EF model changes отсутствуют; dotnet format exit0.
- Production build и OpenAPI drift exit0. CSS~4.26kB, index~112kB, максимальный
  Dashboard chunk357.44kB. Dist fixture-secret check проходит; axe только dev tools.
- Final full HTTPS Docker smoke **91 Chromium tests passed**,0failed/skipped,
  retries0/workers4,3.4m;79 прежних+12 новых. Предыдущий полный run91passed3.4m.
  Все6 миграций применены к новой PostgreSQL17. Проверены оба system roles,
  realMailpit accept/reset/replay и revoked refresh, permissions/status/session
  commands, filters/cache,401/403/409/CSRF и responsive/keyboard. Harness очистил
  свои API/Vite/processes/volumes/env/temporary secrets.
- Targeted acceptance12passed20.4s перед последней semantic/mobile refinement.
  Final40 axe reports:0 confirmed violations, без исключения правил/элементов.
  Review-only findings перечислены ниже; JSON sanitized и ignored.
- 3 screenshot artifacts просмотрены вручную; mobile headings перенесены,
  таблицы скроллятся внутри региона, формы и кнопки доступны, секретов нет.
  Достоверные example PNG в docs/screenshots, остальные test artifacts ignored.
- Secret guard/self-tests/untracked tree и Git whitespace checks exit0.
  npm ci отдельно не повторялся: выполнена actual locked dependency installation.
  Lighthouse/Firefox/WebKit/production image/deployment не запускались.

Промежуточные failures не скрыты: первый Vitest329/330 ожидал старый500 text;
assertion обновлён на новую общую uncertainty policy. Lint выявил Playwright callback
use, ошибочно трактуемый как React hook, и direct setState-effect path; callback
переименован, cleanup реализован через external Query cache без own setState effect.
Первый full smoke27passed/1failed/62not run: изменился initial focus, shell test теперь
проверяет focused heading и Shift+Tab до skip link, сохраняя все skip/trap/Escape checks.
Axe targeted iterations:5passed/7failed (два runs, второй сохранял diagnostics),
8passed/4failed после text/button/toggle fixes,9passed/3failed после Tag tokens,
затем12passed после danger tokens и ожидания завершённого modal/hover state.
Никакие axe rules не выключались. Позже review исправил chart role и mobile title wrap,
проверен итоговый код. Старые flaky lazy-route waits исправлены preloading реального
Dashboard module, не заменой компонента или увеличением assertion timeouts.

## 5. Безопасность

UI capability не заменяет backend authorization. Cookies остаются HttpOnly/Secure/
Strict; unsafe admin calls через central CSRF client. Нет backend security bypass.
Protected cache удаляется на session-check401, приглашение обновляет аудит.
Пароли/токены не пишутся в storage/Query/notifications/console; public link cleanup
и same-document behavior сохранены. Guards не публикуют значения в assertions,
screenshots пустых форм и failure cleanup учитывают password visibility toggle.
Log/dist checks проверяют известные fixture secrets, это не exhaustive secret scanner.
Code не содержит fake production fallback. API/database/crypto architecture сохранена.

## 6. Ограничения / Known issues

Функциональных блокеров11.14 нет.40 scans с0 confirmed violations не означают
сертификацию WCAG. Остались axe incomplete color-contrast (SVG/transparent/clipped
Select/button content) и target-size (collapsed Menu anchor pseudo-element hit areas).
Используются читаемые token foregrounds, стандартные control sizes, Menu rows с
extended hit areas и keyboard/table fallback для графика. Эти проверки требуют
ручной/assistive-technology оценки и явно не считаются автоматически passed.
Первоначальный incomplete aria-prohibited-attr у generic chart div исправлен role=group.
Локальный keyboard/layout review выполнен; screen-reader certification и Lighthouse
не запускались. Только Chromium. Реальные SMTP/VPN/MFA/release CI/proxy/production
build deployment не проверялись и не менялись. JWT downstream живёт до exp; transport
cancellation не отменяет уже завершённый server commit. Ограничения стадий0–13 сохранены.

## 7. Следующий этап

**12 — Production hardening & release**, только по отдельному запросу.
Frontend11.1–11.14 завершён. Git summary/commit на английском; push/deploy не выполняются.

---

# История: этап 11.13 — публичные страницы активации и сброса пароля

**Completed: 11.13**

**Next: 11.14 — только по отдельному запросу пользователя.**

## 1. Этап

11.13 из Identity_Admin_Codex_Addendum_v1.2.md, 20.10.2026. Изучены AGENTS.md,
основной Spec, дополнение, статус, Git, React architecture, generated OpenAPI,
backend validators/handlers и password/token policies. Начальное дерево чистое,
предыдущий commit **8090398** (Demo start), этап11.12 — **30ce1fc**. Сохранены backend0–10, frontend11.1–11.12 и
стандартный Ant Design и локальный demo launcher. Этапы11.14/12 не реализовывались.

## 2. Реализовано

- `features/account/AccountPasswordPage.tsx`: общая типизированная Ant Design
  форма для `/account/activate` и `/account/reset-password`, password/confirmation,
  hint, validation, pending, duplicate-submit guard, timeout30s и abort on unmount.
  Missing/malformed link, generic invalid/expired/consumed, server password policy,
  mismatch, rate limit и uncertain network/server feedback; безопасные h1/live states.
- `accountLink.ts` / `main.tsx`: fragment очищается через history.replaceState
  до React/providers/router. Принимается один token43–512 ASCII base64url chars;
  bootstrap handoff освобождается после commit, совместим со StrictMode. Native
  popstate/hashchange handlers регистрируются до BrowserRouter, очищают URL до его
  чтения и передают новую ссылку прямо форме. Повторное открытие email-ссылки в том
  же document сбрасывает прежний success/error, очищает passwords и отменяет old flight.
  Case/trailing-slash/URI-encoded варианты согласованы с router; encoded slash не
  становится новым сегментом. После handoff observer не хранит token.
- `passwordPolicy.ts`: 15–128 Unicode code points после NFKC, valid Unicode,
  raw1024/normalized512 UTF-8 byte limits; пробелы разрешены, composition rules нет.
  Password/confirmation сравниваются точно; пароль отправляется без Trim/normalization,
  backend остаётся authoritative для denylist и hash/verify normalization.
- `api.ts`: только существующие typed JSON POST invitations/accept и password/reset
  через общий клиент; никаких Query queries/mutations с credentials. Пароли очищаются
  после завершения попытки; token удаляется при success/terminal invalid, сохраняется
  только в памяти формы для явного retry после policy/network failure.
- `app/routes.tsx` / `App.tsx`: публичная ветка вне AuthProvider, поэтому нет admin
  session/CSRF/data requests. Существующие admin/login/guards/REST endpoints сохранены.
  Success без auto-login: обычный пользователь входит через своё приложение; ссылка
  Administrator sign in ведёт на существующий /admin/login и не обещает admin-доступ.
- `account.test.tsx`:31 новых cases для cleanup/history/StrictMode/native navigation,
  anonymous forms, submission/JSON, safe errors, Unicode/size/confirmation, abort,
  timeout, duplicate guard и отсутствия секретов в DOM/storage/Query caches.
  Старый unimplemented-route case /account/activate удалён.
- `e2e/account.spec.ts`:7 scenarios с HTTPS/API/PostgreSQL/Mailpit: invitation →
  anonymous activation → native login → admin reset request → anonymous reset →
  old refresh rejected/old password rejected/new login accepted. Реальный replay
  обеих ссылок, hash cleanup/reload/encoded route, client/server validation, network
  uncertainty, отсутствие admin requests/cookie/storage и console leaks; keyboard
  и empty-form screenshots360/768/1280. Project после audit и перед visual.
- Обновлены root/frontend README, docs/public-account.md, ссылки invitations/reset,
  CHANGELOG и комментарии .env.example с фактическими account URL templates.

## 3. Решения / API / миграции / конфигурация

Generated OpenAPI использует /api/v1/Auth/... с uppercase Auth; сохранена его
типизация, ASP.NET routing case-insensitive. Backend код/DTO/OpenAPI/DB/migrations/
packages/lockfile и runtime defaults не менялись. .env.example получил только
поясняющие комментарии, без нового config flag. SMTP templates уже существуют;
оператор задаёт фактический frontend origin и /account route.

Одноразовый токен не валидируется отдельным GET: сервер проверяет его при POST.
Expired/consumed/invalid объединены намеренно по существующему generic API.
Reload теряет in-memory token; нужно повторно открыть полную ссылку из письма.
Отдельной пользовательской login/registration/forgot-password страницы и user-
controlled redirect нет. Identity Console sign-in предназначен только админам.

## 4. Проверки

### Commands run

Из frontend:

```powershell
npx prettier --write src/features/account src/main.tsx src/app/App.tsx src/app/routes.tsx src/app/routes.test.tsx
npx prettier --write src/features/account e2e/account.spec.ts e2e/shell.spec.ts playwright.config.ts
npx prettier --write README.md docs/public-account.md docs/invitations.md docs/password-reset.md
npm run typecheck
npm run lint
npm run format:check
npm run test:run -- src/features/account/account.test.tsx src/app/routes.test.tsx
npm run test:run
npm run api:check
npm run test:e2e
```

Последние две команды выполняются smoke harness; test:e2e запускает npm run build.
Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet tool restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage-11-13'
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- Итоговые TypeScript/ESLint/Prettier exit0. **324 frontend tests passed**,18files,
  0failed/skipped,18.84s. До финального encoded-route case полный run323passed19.49s;
  прежние runs320/321 также зелёные. Один concurrent run во время browser smoke:
  319passed/3failed на старых Dashboard heading waits в App/routes/auth. Вероятна
  конкуренция за CPU/imports, причинность не инструментирована. Последующие отдельные
  полные runs323/324 прошли с прежними assertions/timeouts, без изменений тех тестов.
  Ранний targeted run обнаружил нестабильное accessible name loading-кнопки:
  добавлен aria-label. Timeout test ожидает async завершение catch. Исправлены
  TypeScript exact option и nullable regex capture, lint require-await/format.
- .NET restore/build exit0,0warnings/0errors. **470 backend tests passed**,
  147Unit/45API/278Integration,0failed/skipped; integration1m32s, реальные PostgreSQL/
  Mailpit. EF pending model changes отсутствуют; dotnet format exit0.
- Production frontend build и OpenAPI drift check прошли. CSS4.13kB, index~111kB,
  максимальный Dashboard chunk357.43kB; bundle secrets check прошёл.
- Итоговый Docker HTTPS smoke: **79 Chromium scenarios passed**,0failed/skipped,
  retries0/workers4,3.1m. 72 прежних+7 новых, включая реальный anonymous Mailpit flow,
  native login/refresh revocation и оба replay. Все6 миграций применены на новой
  PostgreSQL17. Временные API/Vite/Compose volumes/keys/env очищены harness.
  Предыдущий полный smoke79passed3.0m также зелёный; после него добавлена проверка
  URI-encoded path и выполнен ещё один полный smoke.
- Три account PNG360/768/1280 просмотрены вручную: поля/кнопки доступны, текст
  переносится, overflow отсутствует, стандартный Ant Design сохранён. Снимки пустых
  форм, без fragment/password/token; artifacts ignored и не входят в Git.
- Repository secret guard/self-test/untracked и Git whitespace check exit0.

Первый smoke27passed/1failed/51not run: старый shell test ожидал404 на теперь
реализованном /account/activate. Заменён только unknown path на /account/not-implemented.
Второй smoke75passed/1failed/3not run выявил настоящий defect: повторное открытие
ссылки на том же pathname не перезагружает document, bootstrap не запускался и hash
оставался. Добавлены ранние native observers, прямой handoff в форму и component
regression. Assertions hash cleanup/replay не ослаблены; оба повторных полных
Mailpit сценария проходят. Ни один неуспешный промежуточный run не скрыт.

## 5. Безопасность

Credentials только в памяти формы и JSON body HTTPS API, без Query cache/browser
storage/console/analytics/notifications/URL query. Нет получения пароля администратором,
автологина, назначения роли или нового auth flow. Fragment убирается до router и
побочных эффектов; referrer policy уже no-referrer. Bootstrap/observer очищают свои
временные ссылки, терминальные состояния освобождают token, unmount отменяет flight.
Это не обещание физического обнуления JS heap или отмены уже выполненного server commit.
Invalid tokens и server messages не отражаются в UI; common-password policy на backend.
E2E secret-bearing failures sanitized, traces/video выключены, screenshots только
пустых форм; реальные tokens читаются из Mailpit в Node closure без вывода.

## 6. Ограничения / Known issues

Блокеров11.13 нет. Полная UX/security acceptance остаётся11.14, release/proxy/Docker
frontend integration/CI production hardening —12. Проверен Chromium; Firefox/WebKit,
axe/Lighthouse, production image/deployment и реальный SMTP не запускались. npm ci
не повторялся, зависимости не менялись. Существующие ограничения MFA/network access
и JWT до exp сохраняются. Неверные/просроченные/использованные ссылки не различаются
по пользовательским деталям. Локальный harness адаптирует только origin из тестовых
email templates; в эксплуатации оператор обязан настроить реальный origin UI.

## 7. Следующий этап

**11.14 — сквозная доработка frontend и приёмка**, только по отдельному запросу.
Git summary/commit на английском; push/deploy не выполняются.

---

# История: этап 11.12 — журнал аудита

**Completed: 11.12**

**Next: 11.13 — только по отдельному запросу пользователя.**

## 1. Этап

11.12 из Identity_Admin_Codex_Addendum_v1.2.md, 20.10.2026. Изучены AGENTS.md,
основной Spec, дополнение, status, Git, generated OpenAPI, audit validators/projections
и React architecture. Дерево чистое, предыдущий commit **53091e9**. Сохранены
backend0–10, frontend11.1–11.11 и стандартный Ant Design. Этапы11.13+ не начаты.

## 2. Реализовано

- `features/audit/api.ts` / `useAuditEvents.ts`: existing typed GET, AbortSignal,
  page/count normalization, validation DTO/dates/IDs/duplicates, explicit projection.
  Query key содержит все server params, staleTime30s/retry=false; нет client-side
  фильтрации/сортировки/пагинации или fake production data.
- `parameters.ts`: canonical URL page/pageSize/actorUserId/userId/eventType/from/to,
  backend int32 offset limit, nonempty UUID, lowercase/underscore event code<=80,
  ISO timestamps с explicit offset и calendar/range validation. Дробные секунды
  до7 знаков из existing URL сохраняются, включая sub-millisecond range bounds.
- `AuditFilters.tsx` / `localTime.ts`: Ant Form + Row/Col/Input, Apply/Reset,
  local datetime inputs с миллисекундами → ISO UTC; From inclusive, To exclusive.
  Invalid draft не меняет URL/не запускает запрос. URL history/reload восстанавливает
  форму; применение/reset/page size сбрасывают page1. Unknown/invalid URL values
  канонизируются по прежнему принципу каталога; inverted URL period очищается.
- `AuditPage.tsx` / `AuditTable.tsx` / `eventLabels.ts`: read-only таблица OccurredAt,
  Event, Actor, Target/related user, Correlation ID; UTC tooltip, backend order,
  known label + raw event code, unknown event как escaped text, явные null values.
  Valid user IDs → User Details; target предпочитает targetUserId, fallback legacy
  userId явно подписан. Loading/empty/error/GET retry/refresh, first-page recovery,
  external Ant Pagination/page size и keyboard horizontal scroll без page overflow.
- Protected lazy `/admin/audit` в `app/routes.tsx`, активный Menu item/selected state
  в Navigation; обе роли через существующий AuthGate. Safe login return path сохраняет
  только валидированные audit filters. Бывший unimplemented-route test актуализирован.
- `api/queryKeys.ts` / `useUserActivity.ts`: scoped activity array отделён ключом
  audit.userActivity(userId) от paginated audit.list. Иначе одинаковые HTTP query params
  могли смешивать разные формы данных в кеше. Общий audit prefix сохраняет существующую
  инвалидацию. Добавлены component regression и SPA details→filtered audit E2E.
- `parameters.test.ts` / `audit.test.tsx`:33 новых cases, URLs/API casing/time bounds,
  форма/права/pagination/history/loading/zero/error/order/nulls/links/escaping/cache/
  malformed DTO/cancellation. Удалён один устаревший not-found case для /admin/audit.
- `e2e/audit.spec.ts`:8 реальных HTTPS/API/PostgreSQL scenarios: обе роли, сравнение
  row ID order/оригинальных timestamps с response, links, все server filters,
  reload/history/login return, pagination/unknown event zero, injected network/403,
  keyboard scroll/screenshots360/768/1280. После sessions, перед visual, retries0.
- `docs/audit.md`, frontend/root README и CHANGELOG обновлены.

## 3. Решения / фактический контракт / миграции / конфигурация

В backend нет отдельного TargetUserId query: UserId означает target_user_id ИЛИ
legacy user_id. UI явно использует Target / related user, без выдуманного strict-target
filter. DTO AdminAuditEvent не содержит metadata; показывается только Correlation ID
как plain text. Metadata/clientId/extra fields не копируются в новый query cache.
Это уточнения в рамках требования «согласно фактическому API», без расширения API.

Нативный datetime-local через Ant Input не требует новой date library. Пользователь
редактирует local time с миллисекундами; неизменённые ISO URL dates сохраняют все7
fractional digits. Calendar rollover/DST gaps отвергаются; при DST overlap offset
выбирает браузер, а итоговый UTC instant явно хранится в URL. URL defaults/unknown/
invalid filters очищаются, form errors остаются видимыми до исправления.

Domain/Application/Infrastructure/API/DTO/security/DB/migrations/packages/lockfile/
env/config/smoke fixture не менялись. Нового экспорта, редактора JSON и mutations нет.

## 4. Проверки

### Commands run

Из frontend:

```powershell
npx prettier --write src/features/audit src/app/routes.tsx src/app/routes.test.tsx src/components/layout/Navigation.tsx src/features/auth/returnPath.ts src/app/styles.css
npx prettier --write src/features/audit e2e/audit.spec.ts playwright.config.ts
npx prettier --write docs/audit.md README.md
npx prettier --write src/features/audit src/api/queryKeys.ts src/features/users/useUserActivity.ts src/features/users/userDetails.test.tsx e2e/audit.spec.ts docs/audit.md docs/user-details.md
npx prettier --write e2e/sessions.spec.ts e2e/audit.spec.ts
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run api:check
npm run test:e2e
```

Последние две команды выполняет smoke harness; test:e2e вызывает npm run build.
Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet tool restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage-11-12'
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- TypeScript/ESLint/Prettier exit0; **294 frontend tests passed**,17files,0failed/skipped,
  19.20s. Первый lint выявил date helpers exports в component file и лишний async в
  синхронном тесте; исправлены переносом helpers в localTime.ts и удалением async.
  Предыдущие полные Vitest runs также зелёные (293tests,18.97s/22.92s); после cache
  regression294passed. Assertions/timeouts не ослаблялись.
- .NET restore/build exit0,0warnings/0errors; **470 tests passed**,0failed/skipped:
  147Unit/45API/278Integration, реальные PostgreSQL/Mailpit, integration1m25s.
- EF: No changes have been made to the model since the last migration; format exit0.
  OpenAPI types match Identity.Api. Production build exit0: AuditPage8.83kB,
  CSS4.13kB, максимальный chunk357.43kB; warning500kB неизменён.
- Итоговый HTTPS smoke exit0: **72 Chromium scenarios passed**,0failed/skipped,
  retries0/workers4,3.0m:64 прежних+8 новых. Реальные API/PostgreSQL, обе роли,
  исходные timestamps и row order, переходы User Details↔filtered audit без cache
  collision, все filters/history/reload/login return, pagination/zero events,
  manual retry/403 и keyboard/scroll360/768/1280. Три итоговых audit PNG просмотрены
  вручную: form controls/pagination доступны, overflow ограничен таблицей, default
  Ant Design theme сохранена. Все6 миграций применены на новой БД, OpenAPI и dist
  secret checks прошли; harness очистил API/Vite/Compose volumes/temporary secrets.
- Repository secret guard с self-test/untracked и Git whitespace check exit0.

Первый smoke63passed/1failed/8not run: Playwright fill отклонял datetime-local с
нулевыми секундами после нормализации Chromium. Тестовый fill изменён на native
minute format; приложение уже принимает оба формата. Второй smoke60passed/1failed/
11not run остановился в прежнем Sessions keyboard test на1280px: ArrowLeft мог
выполняться до окончания правого scroll. Теперь для всех размеров ожидается движение
вправо, затем несколько клавиатурных шагов влево до0. Проверки scroll/overflow
сохранены, fixed sleep/timeout increase/retries не добавлены. Третий smoke61passed/
1failed/10not run: SPA cache regression начинал ввод до commit pageSize navigation,
и восстановление формы из URL очищало ранний draft. Тест ожидает новый URL и10 rows
перед вводом. Четвёртый smoke65passed/1failed/6not run: network test считал все GET при
первом development mount и получил2 попытки. Lifecycle cancellation/restart в React
StrictMode — вероятное объяснение, точная причина не инструментирована; это не
доказательство automatic query retry. Тест переведён
на Refresh уже загруженной страницы: один failed GET, затем явный manual retry и403.
Полный smoke повторён с новым fixture и актуальными cache keys; итог72passed.

## 5. Безопасность

Новых write controls/unsafe requests нет у обеих ролей. Авторизация остаётся на
backend через существующие cookies/CSRF/role policies; global401/403 очищает protected
view/cache. ID links строятся только из nonempty UUID; raw HTML не используется.
Ни metadata, ни passwords/tokens/unknown response fields не отображаются/не кешируются.
Нет browser storage/console/analytics с credentials. URL whitelist очищает неизвестные
поля и не допускает external login return; period/UUID/event code валидируются.

## 6. Ограничения / Known issues

Metadata и strict-target-only filter отсутствуют в текущем API; UI честно показывает
поддерживаемую семантику. История ограничена retention. Local date editing — browser
millisecond precision, с описанным DST behavior; URL сохраняет higher precision.
Только Chromium; Firefox/WebKit/axe/Lighthouse/production image/deployment не проверялись.
npm ci не повторяется, зависимости не менялись. MFA/network ограничения прежние.

## 7. Следующий этап

**11.13 — Публичные страницы активации и смены пароля**, только по отдельному запросу.
Не начат. Git summary/commit на английском, push/deploy не выполняются.

---

# История: этап 11.11 — управление пользовательскими сессиями

**Completed: 11.11**

**Next: 11.12 — только по отдельному запросу пользователя.**

## 1. Этап

11.11 из Identity_Admin_Codex_Addendum_v1.2.md, 20.10.2026. Изучены AGENTS.md,
основной Spec, дополнение, status, Git и фактические API/handlers/DTO/frontend.
Начальное дерево чистое, предыдущий commit **e644282**. Сохранены backend0–10,
frontend11.1–11.10 и стандартное оформление Ant Design. Следующие этапы не начаты.

## 2. Реализовано

- `features/users/sessions/api.ts`: generated types + существующие GET/DELETE/POST,
  numeric string normalization, validation UUID/dates/unique IDs/page bounds,
  explicit DTO projection. Неожиданные поля не попадают в cache/DOM.
- `useUserSessions.ts`: Query key с userId/page/pageSize/state, AbortSignal,
  staleTime30s, server pagination25 и server state filter all/active/revoked/expired.
- `UserSessions.tsx` / `SessionsTable.tsx`, подключены в `UserDetailsPage.tsx`:
  отдельная секция после профиля, session UUID/client/optional user-agent/state,
  created/lastUsed/expiry/revoked/reason, local time + UTC title. Loading/empty/error/
  manual GET retry, первая страница после filter change, явный возврат с пустой
  поздней страницы. Пагинация и фильтр локальны секции; catalog URL не меняется.
  Доступное keyboard scroll region и штатная Ant Table; CSS только spacing/overflow.
- Два отдельных confirmation: Revoke session в active row с конкретным UUID и
  Revoke all sessions в верхней панели. Без запроса при open/Cancel. Только
  Administrator; ReadOnly не видит write controls/Actions column. Для inactive,
  expired и revoked row действие отсутствует. Revoke all доступен и при пустом
  refresh-списке, поскольку может завершать не показанные здесь admin sessions.
- `useRevokeSessions.ts`: immediate ref double-submit guard, pending, timeout30s,
  unmount cancellation; retry=false/networkMode=always/gcTime=0 по прежней политике.
  После204 обновляются user detail + все его session query keys/dashboard/audit.
  Нет optimistic success или затрагивания другого user's detail cache.
- `SessionCommandError.tsx`: safe403/404/409/CSRF/network/timeout; uncertain completion
  не повторяет POST/DELETE автоматически. Global401/403 оставлен AuthProvider.
  ConfirmAction получил optional afterClose: после успеха focus на Refresh sessions,
  поскольку action отозванной строки исчезает; Cancel сохраняет trigger focus.
- `sessions.test.tsx` + test fixture:24 read/write cases. Новые sessions GET учтены
  в старых details tests, старый E2E актуализирован для Revoke all по роли.
- `e2e/sessions.spec.ts`:10 сценариев с настоящим backend: single/all204 и проверка
  refresh401, независимая другая сессия, отзыв reader admin cookie; реальный ReadOnly403,
  state/empty, injected403/404/409/network, scroll/modal keyboard/focus360/768/1280.
  Новый project после password-reset и перед visual; прежние retries0/workers4.
- `docs/sessions.md`, User details docs, frontend/root README и CHANGELOG обновлены.

## 3. Решения / API / миграции / конфигурация

Используется только существующий per-user sessions API. DTO перечисляет refresh
sessions и isActive, без IP/device/isCurrent для admin browser. Такие поля не
добавлены/не выдуманы. Revoked имеет приоритет, Active берётся из server snapshot;
иначе показывается Expired по expiry либо Inactive. Fresh state — через GET refresh.
Revoke all завершает и refresh-, и admin cookie sessions согласно SessionRevoker;
это явно указано, включая own-session effect. Количество затрагиваемых сессий не
обещается, поскольку page/filter total не включает admin cookies и может устареть.
Внешние JWT остаются потенциально валидными до exp. Domain/Application/Infrastructure/
API/schema/миграции/пакеты/lockfile/config/env/smoke fixture не менялись.

## 4. Проверки

### Commands run

Из frontend:

```powershell
npx prettier --write src/features/users/sessions src/features/users/UserDetailsPage.tsx src/components/ConfirmAction.tsx src/app/styles.css
npx prettier --write src/features/users/sessions src/test/userSessions.ts src/features/users/userDetails.test.tsx
npx prettier --write src/features/users/sessions/sessions.test.tsx e2e/sessions.spec.ts e2e/userDetails.spec.ts playwright.config.ts
npx prettier --write docs/sessions.md docs/user-details.md README.md
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run api:check
npm run test:e2e
```

api:check/test:e2e выполняет smoke harness; test:e2e вызывает npm run build.
Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet tool restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage-11-11'
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- TypeScript/ESLint/Prettier exit0; **262 frontend tests passed**,15files,0failed/skipped,
  18.71s, включая regression pending-refetch focus. В первом run260passed/1failed:
  jsdom не предоставляет ResizeObserver при
  раскрытии Select; добавлен feature-test-only adapter по существующему образцу.
  Повторный полный run зелёный; timeouts/retries/assertions не ослаблялись.
- .NET restore/build exit0,0warnings/0errors; **470 tests passed**,0failed/skipped:
  147Unit/45API/278Integration, реальные PostgreSQL/Mailpit, integration1m24s.
- EF: No changes have been made to the model since the last migration; format exit0.
  OpenAPI types match Identity.Api. Frontend production build exit0, UserDetails22.23kB,
  CSS4.11kB, максимальный chunk357.43kB; warning500kB неизменён.
- Итоговый HTTPS smoke exit0: **64 Chromium scenarios passed**,0failed/skipped,
  0retry,4workers,2.5m:54 прежних+10 новых. Подтверждены DELETE одной сессии/POST всех,
  refresh401 после отзыва, независимая другая сессия, reader cookie revocation,
  audit events, server ReadOnly403, фильтр/empty, safe errors, scroll и keyboard/focus.
  Три viewport PNG360/768/1280 просмотрены вручную: диалоги читаемы, таблица остаётся
  внутри горизонтального scroll region, стандартные Ant Design controls сохранены.
  Все6 existing migrations применены на новой PostgreSQL БД; OpenAPI/dist secret
  checks прошли. Harness очистил API/Vite, Compose volumes и temporary secrets.
- Repository secret guard с self-test/untracked и Git whitespace check exit0.

Первый smoke:58passed/1failed/5not run. Реальные revoke/refresh/ReadOnly сценарии прошли;
keyboard scroll assertion считывал scrollLeft немедленно после ArrowRight, до browser
scroll animation. Заменено на expect.poll до фактического scrollLeft>0, как в прежнем
users.spec.ts; добавлена обратная ArrowLeft проверка. Timeouts/retries не повышались,
проверка scroll/overflow не удалялась. Второй smoke51passed/1failed/12not run нашёл
неустойчивый возврат focus: Refresh button был disabled при background refetch,
а loading icon менял accessible name. Убран лишний disabled (Ant loading сам
блокирует повторный click), добавлен aria-label. Regression test держит GET pending,
проверяет восстановленный focus и отсутствие повторного запроса. Полный smoke
повторён с новой БД, без увеличения timeout/retries; итог64passed.

## 5. Безопасность

Same-origin HttpOnly cookies/CSRF/серверная авторизация не менялись. ReadOnly UI
только читает, backend независимо отклоняет unsafe операции. Mutation variables —
user/session UUID, без tokens/passwords. Read projection исключает unexpected sensitive
поля; React экранирует user-agent/reason. Нет storage/console/analytics с секретами.
Native tokens для E2E находятся только в Node test closure, не в browser JS/URL/storage
или screenshots. Подтверждения объясняют refresh/admin scope и JWT revocation lag.

## 6. Ограничения / Known issues

Отдельной глобальной страницы сессий нет, как требует11.11. Admin cookie sessions
не перечисляются существующим GET; количество всех затронутых сессий неизвестно.
Состояния отражают серверный read snapshot. Исторические сессии ограничены прежней
retention policy; отсутствие rows не означает отсутствие сессий за всю историю.
Только Chromium; Firefox/WebKit/axe/Lighthouse/production deployment/image не проверялись.
npm ci не повторяется: зависимости не менялись. MFA/network ограничения прежние.

## 7. Следующий этап

**11.12 — Журнал аудита**, только по отдельному запросу. Не начат.
Git summary и commit — на английском; push/deploy не выполняются.

---

# История: этап 11.10 — запрос сброса пароля администратором

**Completed: 11.10**

**Next: 11.11 — только по отдельному запросу пользователя.**

## 1. Этап

11.10 из Identity_Admin_Codex_Addendum_v1.2.md, 20.10.2026. Прочитаны AGENTS.md,
основной Spec, дополнение v1.2, текущий status, Git и существующие API/frontend.
Начальное дерево чистое, предыдущий commit **484a7b0** (Ant Design alignment).
Сохранены backend0–10, frontend11.1–11.9, стандартная Ant Design тема и архитектура.

## 2. Реализовано

- `features/users/passwordReset/api.ts`: typed existing POST
  `/api/v1/admin/users/{userId}/password-reset`, без body, через общий API client.
- `usePasswordResetCommand.ts`: TanStack mutation, немедленный ref guard против
  duplicate submit, timeout30s, AbortController/unmount cancellation. Прежние
  retry=false/networkMode=always/gcTime=0; успех только после ответа, без optimistic UI.
  После202 инвалидируется audit family, включая scoped UserActivity.
- `RequestPasswordReset.tsx` + `UserDetailsPage.tsx`: действие только Administrator
  для Active + emailVerified; окончательную eligibility/credentials проверяет сервер.
  Общий Ant ConfirmAction с получателем и объяснением: пароль выбирает пользователь,
  старые reset links становятся недействительными, сессии отзываются при завершении
  reset. Открытие/Cancel не отправляют запрос. Pending блокирует закрытие/повтор.
- Пустой202 → закрытие и «Password reset request accepted for processing.», без
  обещания доставки. Обработаны403/409/network/timeout/CSRF/503 и прочие safe errors.
  Global401/403 по существующему AuthProvider, без login loop или оставшихся данных.
- `passwordReset.test.tsx`:23 component/hook cases, confirmation/cancel, обе роли,
  состояния, отсутствие password form, CSRF/no body, pending/double, audit-only
  invalidation, safe errors, unmount cancellation/storage.
- `e2e/passwordReset.spec.ts`:8 сценариев: реальные202/audit/Mailpit envelope receipt,
  backend ReadOnly403, concurrent disable →409, network failure, global403 и
  keyboard/focus/overflow/screenshot360/768/1280. Без чтения email body/reset token.
  Playwright project после account-status и до visual, workers4/retries0 сохранены.
- `scripts/Test-AdminWebSmoke.ps1`: включён существующий PasswordReset только внутри
  disposable fixture, с тестовым HTTPS fragment email template; production defaults
  неизменны. Старый details E2E обновлён: reset есть у Administrator, отсутствует ReadOnly.
- `docs/password-reset.md`, User details docs, frontend/root README и CHANGELOG
  описывают поведение, настройки оператора, проверки и границы этапа.

## 3. Решения / API / конфигурация / миграции

Требования11.10 совместимы с фактическим backend:202 без DTO, Active+verified+
credentials, transactional encrypted outbox + admin_requested_password_reset.
Новые API/DTO/CQRS/security policies/EF migrations не нужны; backend не менялся.
Новых зависимостей, env variables или production settings нет. Для smoke используются
существующие PasswordReset__Enabled и Email__ResetPasswordUrlTemplate. Никакого
generic command framework: простой feature slice по образцу предыдущих команд.
Запрос ссылки не меняет статус/сессии, поэтому инвалидируется только audit.

## 4. Проверки

### Commands run

Из frontend:

```powershell
npx prettier --write src/features/users/passwordReset src/features/users/UserDetailsPage.tsx e2e/passwordReset.spec.ts playwright.config.ts
npx prettier --write e2e/userDetails.spec.ts docs/password-reset.md docs/user-details.md README.md
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run api:check
npm run test:e2e
```

Последние две команды выполняет smoke harness; test:e2e вызывает npm run build.
Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet tool restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage-11-10'
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- TypeScript/ESLint/Prettier exit0. **238 frontend tests passed**,14files,0failed/skipped,
 9.88s. Исправлена первоначальная TS ошибка в тесте: RTL getByRole не имеет exact option.
- .NET restore/build exit0,0warnings/0errors; **470 tests passed**,0failed/skipped:
 147Unit/45API/278Integration, настоящие PostgreSQL/Mailpit, integration1m27s.
- EF pending-model check: No changes have been made to the model since the last migration.
  .NET format exit0. OpenAPI types match; production frontend build exit0,
  UserDetails chunk14.16kB, максимальный chunk357.43kB, warning limit500kB неизменён.
- Итоговый HTTPS browser smoke exit0: **54 Chromium scenarios passed**,0failed/skipped,
  0retry,4workers,1.8m:46 прежних+8 новых. Подтверждены202 без body/response secrets,
  актуальный audit event, реальное письмо Mailpit, серверный ReadOnly403 и concurrent
  disable →409, injected network/forbidden responses и keyboard/focus/overflow.
  Три PNG reset confirmation360/768/1280 просмотрены вручную; диалог читаем и помещается
  на всех размерах. Стандартные Ant Design controls/theme сохранены.
  Все6 existing migrations применены на пустой БД; dist secret check прошёл;
  fixture API/Vite/Compose volumes/temporary secrets очищены harness finally.
- Repository secret guard (с self-test и untracked) и Git whitespace check exit0.

Первый browser smoke:27passed/1failed/26not run из-за устаревшего assertion11.7,
запрещавшего любую кнопку со словом password. Он заменён точной проверкой роли для
нового reset action; прочие отсутствующие действия и отсутствие незапрошенных команд
по-прежнему проверяются. Полный smoke повторён с новой БД и без retries.
Sandbox не позволил dotnet запуск и Vite helper spawn (EPERM); команды выполнены
повторно с разрешением вне sandbox. Серверные проверки безопасности не отключались.

## 5. Безопасность

Пароль/token/link не вводятся администратором и не возвращаются в UI/DTO, не пишутся
в console, browser storage, analytics или Query cache. Mutation variables — только UUID.
Cookie/CSRF/authorizations прежние, backend решает права и eligibility транзакционно.
UI не повторяет unsafe POST автоматически при сетевом сбое/таймауте/403/CSRF.
Raw server detail/extra secret fields не отображаются; pending не считается успехом.
Mailpit тесты читают только envelope metadata, screenshots не содержат credentials/tokens.

## 6. Ограничения / Known issues

Публичная reset-страница намеренно не реализована (11.13). Запрос ссылки не доказывает
SMTP-доставку и не меняет пароль/сессии. Credentials не входят в admin detail DTO:
последнюю проверку выполняет сервер, а UI корректно принимает409. Только Chromium;
Firefox/WebKit/axe/Lighthouse и production deploy/image не выполнялись. npm ci не
повторяется: package/lockfile не менялись. MFA/network ограничения прежние.

## 7. Следующий этап

**11.11 — Управление пользовательскими сессиями**, только по отдельному запросу. Не начат.
Git summary и commit — на английском, согласно явному запросу пользователя; push/deploy
не выполняются.

---

# История: UI alignment с официальным Ant Design завершён

**Completed: 11.9 + UI alignment (отдельный запрос20.10.2026)**

**Next: 11.10 — только по отдельному запросу пользователя.**

## 1. Задача

Стандартное оформление Ant Design для существующей Identity Console, без новой темы
и без функциональности следующих этапов. Начальное дерево чистое, последний commit
**723097a Block, Unblock**. Прочитаны AGENTS.md/основной Spec/addendum v1.2/status,
текущие React sources и приложенный запрос. Новое указание пользователя заменяет
прежнюю teal theme; архитектура/backend/security contracts не изменяются.

## 2. Реализовано

- `app/theme.ts`: удалены все global/component token overrides и custom palette.
  Только стабильный cssVar scope; установленный Ant Design6.6.6 использует default light
  algorithm автоматически. Стандартные blue/radius/fonts/control32/shadows/state colors.
  Нет dark/compact algorithm, global size override, новой библиотеки/обновления пакетов.
- `app/styles.css`: удалены brand badge, metric border, дублирующий layout CSS,
  aggressive email wrapping, Ant internal class overrides и global motion!important.
  Остались page geometry/overflow/chart fallback/accessibility. Generated appCSS4.02kB
  вместо6.51kB. Свойств цветов в hex/самодельной палитры нет.
- `AdminLayout.tsx`, `PageHeader.tsx`, `Brand.tsx`: стандартные Flex/Space/Typography
  композиции при сохранении Layout/Sider/Header/Content/Menu/Drawer. Header64px/light
  default container surface, content24px/16px mobile/max90rem, прежние navigation states.
  Menu отвечает за selected/hover/icons; собственных component tokens нет.
- `DashboardMetrics.tsx`: Row/Col и стандартные Card/Statistic, без собственной CSS-grid
  системы/size small/акцентной границы. `RegistrationChart.tsx`: tooltip shadow по default
  token вместо none, chart primary/state colors происходят из стандартной темы.
- `UsersPage.tsx`: Form.Item labels и Row/Col для controlled filters, Space/Flex для
  действий/footer. URL/debounce/filters/sort/page boundaries остаются источником состояния.
- `UsersTable.tsx`: стандартный default size/hover/header/sort; email/name ellipsis/native
  title, достаточная ширина110px и nowrap для Details, keyboard scroll min920px.
  Стандартные Pagination/Select остаются вне scroll region для mobile accessibility;
  Empty тоже снаружи, чтобы не центрировать empty внутри широкой невидимой таблицы.
- `InviteUser.tsx`: стандартный Modal footer вместо custom body buttons. Default520px,
  Form vertical/default spacing, native form association через React useId/okButtonProps.
  Enter/validation/submit/double guard/pending/form clearing/roles/CSRF/202/409 сохранены.
- `ErrorState.tsx`: Ant Result вместо custom icon/title layout с прежними safe messages,
  trace reference и ручным GET retry. EmptyState использует обычный Ant Text в Empty
  description с semantic heading, без oversized display heading.
- `e2e/antDesign.spec.ts`:3 real-backend visual cases, каждый снимает Dashboard/Users/
  Invite на360/768/1280px. Проверяются computed default blue/control32/button radius6,
  email ellipsis, default Modal footer, focus return и page overflow. Visual project
  после прежних lifecycle suites, workers4/retries0; тестовых данных в production нет.
- Новая docs/ant-design-alignment.md содержит UI audit, ссылки на official docs,
  принятые решения и повторение visual checks. README/root README/CHANGELOG и docs shell/
  users/invitations актуализированы. Новых settings нет, env samples сохранены.
- Business API/httpClient/CQRS/security policy/schema/DB/migrations/solution/dependencies/
  lockfile/production Docker/CI не менялись. Этапы11.10+ не начаты.

## 3. Решения

Installed antd6.6.6 соответствует версии, указанной официальным сайтом на момент работы.
ConfigProvider/Ant App/reset.css уже были корректны, Bootstrap/Tailwind/сторонних resets
не было. Визуальные отличия создавали token overrides, а не отсутствие Ant CSS.
Поэтому изменены только theme/composition, без замены рабочих форм/запросов.
Для светлого header используется стандартный colorBgContainer локально; это выбор
surface в Layout, не override theme. Внешняя Ant Pagination сохраняет int32 offset,
custom pageSize и доступность mobile Rows per page; никакой собственной пагинации нет.

Изучены official Overview/Layout/Modal/Table/Customize Theme; в браузере просмотрены
страница Layout и Basic Modal. В каталоге вложения только Pasted text.txt, изображения
референса нет; сравнение выполнялось с официальными примерами, без утверждения pixel match.
Ссылки и описание — docs/ant-design-alignment.md.

## 4. Проверки

### Commands run

Из frontend:

```powershell
npm run format
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run test:run -- src/app/App.test.tsx src/app/routes.test.tsx src/features/auth/auth.test.tsx
npm run api:check
npm run build
npm run test:e2e
```

Последние3 команды выполнены smoke harness (test:e2e также вызывает build/typecheck).
Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=ant-design'
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- TypeScript/lint/Prettier exit0; **215 frontend tests passed**,0failed/skipped,13files,
  последний полный прогон9.50s. Existing business/transport/auth/modal/navigation cases
  не удалялись и assertion timeout/retries не повышались.
- Итоговый HTTPS smoke exit0: **46 Chromium scenarios passed**,0retry,4workers,1.4m
  (43 прежних+3 visual). Реальные
  PostgreSQL17/Mailpit/API/Vite HTTPS, обе роли, все прежние login/logout/catalog/details/
  invite/resend/disable/enable сценарии. OpenAPI types match,6 migrations применены;
  processes/Compose volumes/temporary secrets очищены harness finally. Dist проверен
  на отсутствие disposable secrets. Production build exit0, max chunk357.43kB,
  warning500kB не менялся. Details11.64kB/users10.82kB/app103.15kB.
- Девять итоговых PNG Dashboard/Users/Invite на360/768/1280 просмотрены вручную и
  сопоставлены с official default components. Исправлены View details wrapping,
  tablet filter widths и capture до завершения enter animation. Snapshot-файлы —
  ignored test-results/antDesign-*; screenshot capture ожидает stable opacity/конец
  enter classes, production animations сохранены. Это не axe/Lighthouse/pixel diff.
- Backend restore/build exit0,0warnings/0errors. **470 tests passed**,0failed/skipped:
  Unit147/API45/Integration278 с настоящими PostgreSQL/Mailpit; integration1m33s.
  EF: No changes have been made to the model since the last migration; .NET format exit0.
- Secret guard и whitespace check exit0. npm ci, API Docker image build и production
  frontend image не повторялись: пакеты/backend неизменны, production hosting — этап12.

Первый полный frontend run:212passed/3failed на1s RTL wait lazy Dashboard, duration76.90s
с необычно медленной загрузкой modules. Причина связана с cold import/resource pressure
по наблюдаемому timing, это inference. Те же3 файла отдельно30passed; затем два полных
прогона215passed (9.74s/9.50s), без ослабления assertions/timeout. Browser smoke все сценарии
проходил, но manual visual review нашёл capture enter frame (одна mask без Modal) на
некоторых снимках: capture animations disabled сам по себе недостаточен в prepare frame.
Добавлено явное ожидание завершения rc-motion enter/opacity1, затем smoke повторён.
Первые два browser runs также46passed; повторение необходимо для verification PNG.
NO_COLOR/FORCE_COLOR warnings runner косметические; SELECT migration history до создания
пустой DB ожидаем. API бизнес-логика ради тестов не менялась.

## 5. Безопасность

Не изменены cookie/CSRF/authorization/global401/403, confirmations, dangerous actions,
AbortSignal/timeout/duplicate guards. Нет secrets в UI/URL/storage/logging/bundle.
Modal footer связан с формой, а не обходит validation; permissions всё ещё решает сервер.
Email эллипсис не скрывает полный адрес из accessible cell/title или details. Тестовые
изображения содержат только disposable accounts/blank fields, не credentials/tokens.

## 6. Ограничения / Known issues

- Пользовательский reference screenshot отсутствовал; использовались live official docs.
- Только Chromium; Firefox/WebKit/axe/Lighthouse/production deployment не проверялись.
  Local visual review не является pixel-identical копией documentation website.
- PNG находятся в ignored test-results и перезаписываются следующим smoke; воспроизведение
  описано в docs. Бизнес-этап11.10 и дальнейшие остаются впереди. MFA/network ограничения
  прежние. Новых блокеров не обнаружено; commit/push/deploy не выполнялись.

## 7. Следующий этап

**11.10 — Запрос сброса пароля администратором**, только по отдельному запросу. Не начат.

---

# История: этап 11.9 завершён

**Completed: 11.9**

**Next: 11.10 — только по отдельному запросу пользователя.**

## 1. Этап

**11.9 — Блокировка и разблокировка пользователя**, 2026-10-19.
Изучены AGENTS.md, основной Spec, дополнение v1.2, текущий status, Git и реальные
OpenAPI/Controllers/validators/domain transitions. Начальное дерево чистое;
11.8 зафиксирован commit **1d0626a**. Backend0–10 и frontend11.1–11.8 сохранены.

## 2. Реализовано

- `features/users/accountStatus/api.ts`: existing typed POST disable с generated
  DisableUserRequest.reason и enable без body; общий same-origin cookie/CSRF client,
  AbortSignal, пустой204. Нет новых API/DTO и произвольной смены status.
- `useAccountStatusCommand.ts`: две конкретные TanStack-команды, immediate ref guard,
  timeout30s, AbortController до mutation scheduling/unmount cancellation; прежние
  retry=false/networkMode=always/gcTime=0. Success только после HTTP, без optimistic status.
- `AccountStatusAction.tsx`: действия только в User Details для Administrator.
  Disable для Active/PendingVerification/Invited, Enable только Disabled; Deactivated/
  unknown status и ReadOnly без controls. Последнего администратора определяет backend.
- Отдельные ConfirmAction для Disable/Enable, target email, Cancel initial focus,
  подтверждение/keyboard/Escape/focus restoration. Disable объясняет отзыв refresh/admin
  sessions, запрет sign-in/refresh, invalidation одноразовых ссылок и внешний JWT expiry lag.
  Enable поясняет, что восстановленный status задаёт Identity; старые сессии/ссылки
  не восстанавливаются. Нет HTTP при открытии/Cancel.
- Reason required, max500, trim при submit, control characters/line breaks отклоняются
  как backend validator; Ant Form/TextArea. Закрытие очищает форму; Cancel во время
  async validation предотвращает ещё не начатую команду. Pending блокирует fields/
  confirm/cancel/close, повторный confirm защищён до React render.
- `StatusCommandError.tsx`: last_administrator409, state409, validation400/reason,
  CSRF/403/404/429/network/timeout. Только безопасные сообщения, raw ProblemDetails
  detail/field text и произвольные exception messages не выводятся. Network outcome
  неизвестен: refresh перед осознанным повтором, автоматического POST replay нет.
- После204 toast, закрытие confirmation и invalidate users.all (catalog/detail/все
  cached session pages, включая inactive), dashboard/audit. Auth cache не инвалидируется;
  новые GET определяют status. Existing global401/403 cleanup/login/Forbidden сохранены.
- `UserDetailsPage.tsx`: подключение action рядом с существующими resend/refresh.
  `styles.css`: только geometry формы с theme tokens. Read-only profile DTO projection
  не расширяется reason/прочими неиспользуемыми полями.
- `features/users/validation.ts`: существующая char.IsControl-проверка приглашения
  вынесена для повторного использования reason form; поведение Invite не меняется.
- `accountStatus.test.tsx`:31 новых component/hook cases. Тестовый ResizeObserver только
  для jsdom textarea layout; production/browser implementation не подменяется.
- `e2e/accountStatus.spec.ts`:8 новых serial scenarios. Настоящие disable/enable204,
  native sign-in200→401→200, revocation отдельной reader admin-cookie, Invited→Disabled→
  Invited, реальный last-admin409, ReadOnly и injected403/Forbidden, network failure,
  validation/Tab/Escape/Cancel/focus/screenshots360/768/1280. Прежний detail E2E обновлён
  для появления Disable у Administrator; ReadOnly без actions.
- Playwright status project зависит от invitation project, после dashboard baseline
  checks; workers4/retries0 сохранены. Временные reader/invited accounts из fixture,
  reader восстанавливается в успешном lifecycle scenario; DB полностью disposable.
- `scripts/Test-AdminWebSmoke.ps1`: только fixture включает existing Jwt feature с
  отдельным ephemeral RSA3072 PEM (HTTPS key не переиспользуется). Нативный вход
  проверяется без чтения/печати response tokens. Ключ удаляется existing finally cleanup,
  environment восстанавливается. Product defaults не меняются.
- Root/frontend README, CHANGELOG, docs/authentication/shell/users/user-details и новый
  docs/account-status.md обновлены. IMPLEMENTATION_STATUS обновлён.
- Backend/CQRS/API/OpenAPI schema/DB/миграции/solution/security policies, dependencies,
  lockfile, env samples, Docker images/CI не менялись. Новых product settings нет.

## 3. Решения

ТЗ11.9 разрешает actions в карточке и/или каталоге: выбрана карточка, где видны актуальные
status/email. Используются существующие Ant theme/ConfirmAction и transport/query keys.
Небольшой feature hook содержит только disable/enable; универсальный CRUD не создавался.
Единый users prefix инвалидирует также inactive session keys без внедрения sessions UI
раньше11.11. UI не знает admin count и не подменяет серверный last-admin guard.

Enable не назначает Active: fresh GET может вернуть Invited/PendingVerification. Backend
idempotency сохраняется; frontend показывает только допустимую команду для текущего
DTO. Причина проверяется по реальному более строгому контракту (без control characters).
Это уточнение отображено в helper text, серверные требования не изменены.
Расхождений/блокеров API нет. Новых стадий не реализовано.

## 4. Проверки

### Commands run

Frontend:

```powershell
npm run format
npm run typecheck
npm run lint
npm run format:check
npm run test:run -- src/features/users/accountStatus/accountStatus.test.tsx
npm run test:run
npm run api:check
npm run build
npm run test:e2e
```

Последние три команды — внутри HTTPS smoke (test:e2e включает build/typecheck).
Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage11-9'
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- Итоговые frontend typecheck/lint/Prettier exit0. **215 tests passed**,0failed/skipped,
  13files (184 прежних +31 новых), финальный suite9.27s. Проверены status/role gating,
  reason limits/control/whitespace, async validation Cancel, confirmation,204/403/409,
  CSRF/404/429/network, double-submit, safe errors, no optimistic data, cached inactive
  sessions invalidation и abort до/во время HTTP.
- Итоговый HTTPS smoke exit0: **43 Chromium scenarios passed**,0retry,4workers,1.2m;
  35 прежних +8 новых. Предыдущий успешный прогон также43passed; после visual fix на360px
  повторены frontend suite и весь smoke. Реальные PostgreSQL17/Mailpit/API/Vite TLS,
  оба administrative roles, native login блокируется после Disable, reader cookie
  отвергается при следующем refresh. Все6 существующих миграций применены. Own processes,
  Compose volumes, ephemeral JWT/HTTPS/outbox keys очищены; тестовых секретов в dist нет.
- OpenAPI types match. Production build exit0, без500kB warning: details11.65kB
  (gzip4.31), users10.69kB(gzip4.46), max chunk357.34kB. Dev browser smoke не подтверждает
  production hosting. Снимки360/768/1280 просмотрены; после removal счётчика валидатор
  не теснится с ним на mobile, форма/кнопки читаемы, page overflow отсутствует.
- .NET restore/build exit0, **0warnings/0errors**. **470 tests passed**,0failed/skipped:
  Unit147/API45/Integration278 с real PostgreSQL/Mailpit; Integration1m32s. TRX stage11-9
  находятся в ignored TestResults. Backend после прогона не менялся.
- EF: No changes have been made to the model since the last migration. .NET format exit0.
  Secret guard с SelfTest/IncludeUntracked и whitespace check exit0.
- npm ci/API Docker image build не повторялись (зависимости/backend без изменений).
  Production frontend Docker image, release deployment и CI hardening — этап12.

Первичный component run обнаружил отсутствие ResizeObserver в jsdom для AutoSize TextArea;
добавлен test-only stub, затем весь suite успешно выполнен. Первый smoke остановился на
native login503: прежний fixture выключал Jwt. Existing feature включён только в disposable
среде с отдельным временным ключом. Следующие два smoke выявили неверное ожидание Dashboard
после смены роли в новом тесте: реальный login сохранял account return path, включая reload
того же URL/history.state. Тест теперь явно выбирает protected Dashboard до нового входа;
существующий auth flow не менялся. После исправления все43passed. Visual review нашёл
тесное соседство TextArea counter и validation на360px; optional counter убран, лимит/
helper сохранены, все43passed повторно. NO_COLOR/FORCE_COLOR warnings косметические;
первичный SELECT migration history на пустой DB ожидаем, все миграции применяются.

## 5. Безопасность

Cookie/CSRF и серверная роль/статус/session authorization сохраняются; ReadOnly controls
отсутствуют. Последнего администратора защищает существующая атомарная backend проверка,
проверенная настоящим409. Нет роли-management API, произвольного редактирования status,
паролей/tokens/links в UI/cache/storage/logs. Mutation variables — только userId/reason;
reason нужен только форме/команде, не добавляется в profile projection или URL. Не следует
вводить credentials/лишнюю PII в reason. Confirmation/ref guard/timeout/abort/retry=false
ограничивают случайные и скрытые повторы. При неопределённой сети UI не обещает результат.
Сессии/одноразовые ссылки отзывает backend, downstream JWT expiry lag явно поясняется.
Secret guard проверяет current tree, не полную Git history/все виды секретов.

## 6. Ограничения / Known issues

- После Enable статус определяется свежим GET; прежние сессии и ссылки не возобновляются.
  Если был Invited, для нового приглашения нужен явный resend через уже готовый11.8.
- Новый account-status browser suite зависит от invitation fixture; общий harness
  запускает проекты в нужном порядке и очищает все данные.403 UI scenario инжектирован;
  реальный запрет ReadOnly mutations остаётся в backend integration suite.
- Password reset11.10, sessions UI11.11, полный audit11.12 и public account pages11.13
  не реализованы. Prod image/reverse proxy/CSP/release CI —12. До MFA restricted network.
- Firefox/WebKit/axe/Lighthouse/production deployment не выполнялись. Известных блокеров
  11.9 нет. Commit/push/deploy не выполнялись. Env samples не требуют изменений.

## 7. Следующий этап

**11.10 — Запрос сброса пароля администратором**, только по отдельному запросу. Не начат.

---

# История: этап 11.8 завершён

**Completed: 11.8**

**Next: 11.9 — только по отдельному запросу пользователя.**

## 1. Этап

**11.8 — Приглашение и повторная отправка приглашения**, 2026-10-19.
Прочитаны AGENTS.md, основной Spec, дополнение v1.2, текущий статус, Git и существующие
OpenAPI/Controllers/validators/handlers. При первом чтении оставались изменения11.7;
в ходе работы они зафиксированы commit92e8908 User card. Существующие изменения сохранены.
Реализован только11.8; backend0–10 и frontend11.1–11.7 сохранены.

## 2. Реализовано

- `features/users/invitations/api.ts`: существующие POST invite/resend, generated DTO,
  общий typed same-origin client с cookie/CSRF и пустым202. Нет новых API.
- `useInvitationCommand.ts`: TanStack mutation для двух конкретных команд; моментальная
  защита ref от double submit, timeout30s, AbortController создан до запуска мутации,
  отмена при unmount даже до начала HTTP. Прежние retry=false/networkMode=always/gcTime=0.
- `InviteUser.tsx`: кнопка на Users только для Administrator, Ant Modal/Form email required
  до320, optional display name до120, проверка control characters, trim/null. Получатель
  сам задаёт пароль; Send invitation явно подтверждает запрос. Pending блокирует поля,
  submit/cancel/close. Форма уничтожается при закрытии; Cancel не отправляет запрос.
- `ResendInvitation.tsx`: только Administrator + Invited; общий ConfirmAction с пояснением
  отзыва старых invitation links, Cancel в начальном фокусе, pending/double guard.
- `InvitationError.tsx`: safe validation/duplicate409/state409/404/429/503/CSRF feedback;
  network/timeout означает неизвестный результат и предлагает проверить Users/activity.
  Нет автоматического replay, raw server detail или произвольного exception.message.
- После202 toast Invitation accepted for processing без обещания доставки; invite
  закрывает форму и инвалидирует users/dashboard; resend закрывает confirmation и
  инвалидирует конкретный detail/audit. Обновляются активные GET, без SPA reload.
- `UsersPage.tsx`, `UserDetailsPage.tsx`: встраивание действий в существующий layout.
  `styles.css`: только responsive geometry с theme tokens. `containTabFocus.ts` учитывает
  input/select/textarea для клавиатурного обхода формы; Ant Modal возвращает фокус trigger.
- `invitations.test.tsx`:22 новых component/hook cases. `test/TestAuth.tsx` — test-only
  provider существующего AuthContext; прежние catalog/detail tests читают как ReadOnly.
- `e2e/invitations.spec.ts`:7 real-backend scenarios: invite, Invited row, resend/audit,
  два Mailpit receipts, ReadOnly, real409, network failure, keyboard/focus360/768/1280.
  Старый users test теперь ожидает Invite только у Administrator.
- `playwright.config.ts`: отдельный invitation project зависит от прежнего Chromium suite,
  чтобы записи в общей disposable DB не гонялись с dashboard baseline assertions.
  Invitation scenarios serial; workers4/retries0 сохранены.
- `scripts/Test-AdminWebSmoke.ps1`: existing Invitations/фиксированный email template
  включаются только в ephemeral fixture; server-only IDENTITY_E2E_MAILPIT_URL для проверок
  envelope metadata. Mail bodies/links/tokens не читаются новыми browser tests.
- README/CHANGELOG/frontend README/docs users/details/shell/authentication и новый
  docs/invitations.md обновлены. Конфиги-примеры не менялись: новых product settings нет.
- Backend, CQRS, security policies, OpenAPI schema, DB/миграции, solution, dependencies,
  lockfile, Docker images/CI не менялись. Новых библиотек/секретов нет.

## 3. Решения

Первый write flow использует существующий feature API и Query infrastructure, Ant Design
theme и controlled confirmation. Один небольшой hook содержит только две invitation
команды; универсального CRUD/mutation framework нет. Значения формы не попадают в URL
или browser storage. Password/token вообще не входят в контракт. Form submit служит
явным подтверждением invite (§6 дополнения); resend требует отдельного подтверждения.

После команды UI показывает факт202, а не доставку. При сетевой неопределённости
автоматический повтор запрещён; для resend409 требуется refresh изменившегося аккаунта.
Фильтры каталога сохраняются: новый аккаунт может не соответствовать текущему фильтру.
API полностью совместим, изменения backend/согласования контрактов не потребовались.

## 4. Проверки

### Commands run

Frontend:

```powershell
npm run format
npm run typecheck
npm run lint
npm run format:check
npm run test:run -- src/features/users/invitations/invitations.test.tsx
npm run test:run
npm run api:check
npm run build
npm run test:e2e
```

Последние три выполняются HTTPS smoke script (test:e2e также включает build).
Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage11-8'
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- Итоговые TypeScript/lint/Prettier exit0; **184 frontend tests passed**,0failed/skipped,
  12files (162 прежних +22 новых). Validation/202/409/readonly/double-submit acceptance
  покрыт, включая field errors, null name, cache invalidations и abort до/во время HTTP.
- Итоговый HTTPS smoke exit0: **35 Chromium scenarios passed**,0retry,4workers,42.2s,
  включая7 новых (первый прогон также35passed,44.9s).
  Реальные API/Vite TLS/PostgreSQL17/Mailpit, обе роли, два доставленных invitation emails,
  реальный409, обновлённая Invited row и admin_resent_invitation. OpenAPI types match.
  Применены все6 миграций, temporary processes/volumes/keys/env очищены harness finally.
  Снимки360/768/1280 просмотрены: форма помещается, фокус/Tab/Escape/возврат работают.
- Production build exit0, без500kB chunk warning; все chunks меньше358kB. Нет тестовых
  секретов в dist. Browser tests используют dev HTTPS proxy, не production hosting.
- .NET restore/build exit0, **0warnings/0errors**. **470 backend tests passed**,
  0failed/skipped: Unit147/API45/Integration278, real PostgreSQL/Mailpit; Integration1m29s.
- EF: No changes have been made to the model since the last migration. .NET format,
  secret guard с SelfTest/IncludeUntracked и whitespace check exit0.
- npm ci/API Docker image build не повторялись (зависимости/backend без изменений).
  Production frontend image, release deployment и CI hardening — этап12.

Первичные TypeScript/lint проверки нашли неверный порядок аргументов существующего
client, widened form field types, control-regex и test-only typing/async issues; исправлены.
Первый component run требовал ожидания видимости Ant Modal в jsdom; исправлены ожидания
и стабильное accessible name pending submit. После исправлений полный suite зелёный.
Первый HTTPS smoke также прошёл35 cases; после дополнительной защиты раннего abort
выполнены полный frontend suite и повторный smoke. NO_COLOR/FORCE_COLOR warnings runner
косметические; SELECT migration history до создания чистой DB ожидаем.

## 5. Безопасность

Authorization остаётся на сервере, UI использует canWrite только для отсутствия controls.
Центральные same-origin cookie/CSRF и global401/403 остаются прежними. Нет паролей,
tokens/links/raw errors в UI/cache/storage/logs; mutation variables содержат только
несекретный input email/name либо userId. Form values очищаются при закрытии, inactive
mutation cache gcTime0. Ref guard/timeout/abort/retry=false исключают скрытые повторы.
Одноразовые токены и outbox encryption остаются server-side. Mailpit assertions читают
только envelope metadata; новые скриншоты содержат пустую форму и disposable account data.

## 6. Ограничения / Known issues

- 202 не гарантирует SMTP delivery; retry после сети — только осознанное действие оператора.
- Public activation UI —11.13. До неё получатель не может завершить приглашение через SPA;
  существующий public acceptance API сохранён. Password assignment не добавлен.
- Disable/enable11.9, reset11.10, sessions11.11, полный audit11.12 не реализованы.
- Firefox/WebKit/axe/Lighthouse/production deployment не проверялись. До MFA требуется
  restricted admin network. Продуктовые версии и env samples остаются прежними.
- Известных блокеров11.8 нет. Commit/push/deploy не выполнялись агентом.

## 7. Следующий этап

**11.9 — Блокировка и разблокировка пользователя**, только по отдельному запросу. Не начат.

---

# История: этап 11.7 завершён

**Completed: 11.7**

**Next: 11.8 — только по отдельному запросу пользователя.**

## 1. Этап

**11.7 — Карточка пользователя: только чтение**, 2026-10-19.
Изучены AGENTS.md, основной Spec, дополнение v1.2, текущий status, Git, реальные
detail/audit DTO и semantics серверных фильтров. Начальное дерево чистое;
11.6 зафиксирован commit **7c46690**. Backend0–10 и frontend11.1–11.6 сохранены.

## 2. Реализовано

- `src/Identity.Admin.Web/src/features/users/detailsApi.ts`: typed existing GET
  details и audit, DTO из generated OpenAPI, runtime validation и явная проекция
  только полей экрана. Проверяются соответствие user ID, даты и принадлежность events.
- `useUserDetails.ts`, `useUserActivity.ts`: существующие keys users.detail и
  audit.list({Page:1,PageSize:10,UserId}); AbortSignal, staleTime30s/networkMode always,
  прежний retry=false. Audit загружается только после успешного detail GET.
- `UserDetailsPage.tsx`, `UserProfile.tsx`: защищённая lazy карточка `/admin/users/:userId`.
  Email, nullable display name, status, email verification, created/updated/verified/
  disabled/deactivated timestamps; общий StatusTag/LocalDateTime и Ant Descriptions.
  Явные null labels, локальное en-US время и UTC title. Отдельные invalid/zero UUID,
  loading, user404, safe error/manual GET retry и refresh. Нет изменяющих действий.
- `CopyUserId.tsx`: keyboard-accessible Clipboard API только по нажатию, pending,
  live success/failure; при отказе можно выделить видимый ID. Без DOM fallback,
  clipboard errors/logs и копирования остального профиля.
- `UserActivity.tsx`: последние10 retained событий пользователя через серверный
  UserId filter, порядок backend. Отображаются только eventType/occurredAt; independent
  loading/empty/error/retry/refresh. Ошибка activity не скрывает профиль, failed refresh
  скрывает stale данные своей секции. Нет arbitrary metadata или полного audit feed.
- `userId.ts`: nonempty UUID guard до HTTP. Details route/запросы нормализуют UUID case.
- UsersTable links переносят allowlisted catalog filters в query карточки вместо
  router state. Breadcrumb Users/Back to users строят явный URL каталога; reload/direct
  load сохраняет фильтры. returnPath принимает их также для валидного detail UUID,
  сохраняя запреты open redirects/fragment/arbitrary query. Невалидные query canonicalize
  replace как в11.6. Users navigation остаётся выбранным.
- Общая CSS geometry: Descriptions1/2/3 колонки, перенос ID/текстов, адаптивный список
  событий, Refresh отдельно от mobile heading. Никаких новых цветов/UI kit.
- `userDetails.test.tsx`/`test/userDetails.ts`:24 новых unit/component cases; прежние
  catalog/navigation tests адаптированы к рабочему details route и query links.
- `e2e/userDetails.spec.ts`:7 новых real-backend scenarios — обе роли, SPA переход,
  clipboard по Enter, actual profile/activity, reload/back filters, invalid ID/404,
  independent activity network failure/retry,360/768/1280 screenshots/overflow/storage.
- Root/frontend README, CHANGELOG, docs/users/authentication/shell и новый
  docs/user-details.md. IMPLEMENTATION_STATUS обновлён.
- Backend/CQRS/API/OpenAPI schema/DB/миграции/solution/security policies/env samples/
  Docker/CI, dependencies и lockfile не менялись. Новых настроек/библиотек нет.

## 3. Решения

ТЗ11.7 допускает activity только при наличии серверного фильтра: фактический UserId
матчит legacy user_id OR target_user_id. Используется один ограниченный GET на10 events,
без client-side поиска по полному аудиту. Это события об аккаунте, не все действия
аккаунта как actor; существующая retention policy применяется. В cache не сохраняются
неиспользуемые поля detail/audit, включая reason/actors/correlation/clientId/metadata.

Фильтры остаются в URL карточки: возврат работает без sessionStorage/router state
после reload и login. Query hooks отделены от UI, общие factories/error states/theme
переиспользованы. Обе роли читают одинаковый экран; backend решает authorization.
Не добавлены заглушки write controls, sessions section или глобальный audit screen.
Расхождений, требующих изменения API, нет.

## 4. Проверки

### Commands run

Из frontend:

```powershell
npm run format
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run build
npm run api:check
npm run test:e2e
```

Последние три команды выполнены внутри HTTPS smoke script (build включает typecheck).
Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage11-7'
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- Итоговые typecheck/lint/Prettier exit0. **162 frontend tests passed**,0failed/skipped,
  11files (138 прежних+24 новых). Проверены null/status variants/404, URI filters/back,
  безопасный transport/projection/escaping, отсутствие sensitive fields в DOM/cache,
  loading/empty/manual retry, clipboard success/denial, abort detail/activity, malformed
  DTO/неверный user ID/посторонний audit event и скрытие stale account после error.
- Итоговый HTTPS smoke exit0: **28 Chromium scenarios passed**,0retry,4workers,23.2s;
  семь новых и21 прежний. Реальные PostgreSQL17/Mailpit/API/CLI/Vite TLS proxy. OpenAPI
  types match; все6 существующих миграций применены. Browser проверяет actual clipboard,
  обе роли и сохранение фильтров после reload. Снимки360/768/1280 просмотрены; mobile
  heading не обрезается, page overflow отсутствует. Disposable processes/Compose/
  volumes/keys очищены скриптом, тестовых секретов в dist нет.
- Production build exit0, без500kB warning: UserDetailsPage6.23kB(gzip2.30),
  UsersPage6.76kB(gzip2.69), app104.74kB(gzip34.64); max chunk357.34kB.
  Общие vendor chunks остаются прежними cache boundaries, могут preload с shell.
- Backend restore/build exit0, **0warnings/0errors**. **470 tests passed**,
  0failed/skipped: Unit147/API45/Integration278 с настоящими PostgreSQL/Mailpit.
  Integration1m30s; TRX stage11-7 — в ignored TestResults.
- EF: No changes have been made to the model since the last migration. .NET format,
  secret guard и whitespace check exit0. npm ci/API Docker image build не повторялись:
  зависимости/backend не менялись. Production frontend image не создавался (этап12).

Первичный typecheck выявил unsupported Ant Result status400 и два unused test imports;
исправлены warning presentation/импорты. Первый smoke также прошёл все28 тестов, но
визуальная проверка нашла обрезание activity heading на360px: Refresh перенесён в тело
карточки, затем успешно повторены applicable frontend checks и весь smoke.
NO_COLOR/FORCE_COLOR warnings runner косметические. SELECT __EFMigrationsHistory на
пустой DB ожидаемо предшествует её созданию; все миграции затем успешно применены.

## 5. Безопасность

Никаких административных мутаций, password/token/hash/raw metadata в DOM/cache,
credentials persistence или browser storage. API validation/DTO projection и React text
escaping ограничивают обработку неожиданного ответа. UUID guard, scoped audit filter,
bounded page и AbortSignal сохранены; late responses не подменяют текущую карточку.
Copy касается только несекретного UUID. Cookie/CSRF/global401/403/server policies прежние.
Поисковые фильтры email/name видны в URL/history согласно ТЗ; analytics отсутствует.
Secret guard проверяет текущее дерево, а не всю Git history/все форматы секретов.

## 6. Ограничения / Known issues

- Активность ограничена последними10 retained событиями про пользователя/target;
  не обещается полный архив или actor history. Полный журнал —11.12.
- Clipboard может быть запрещён browser policy: показан fallback для ручного выделения
  ID. Даты локальные en-US; UTC доступен через title.
- Invite/resend11.8, disable/enable11.9, reset11.10, sessions11.11 и остальные следующие
  подэтапы не реализованы. Public account pages/production hardening остаются далее.
- Firefox/WebKit/axe/Lighthouse/production deployment не проверялись. Restricted network
  requirement до MFA сохраняется. Новых env samples не требуется: настроек не добавлено.
- Известных блокеров11.7 нет. Commit/push/deploy не выполнялись.

## 7. Следующий этап

**11.8 — Приглашение и повторная отправка приглашения**, только по отдельному запросу.
Не начат.

---

# История: этап 11.6 завершён

**Completed: 11.6**

**Next: 11.7 — только по отдельному запросу пользователя.**

## 1. Этап

**11.6 — Пользователи: таблица и поиск**, 2026-10-19.
Изучены AGENTS.md, основной Spec, дополнение v1.2, текущий status, Git и реальные
OpenAPI/DTO/validator. В начале дерево чистое; 11.5 зафиксирован commit **f0bec7a**.
Реализован только каталог; backend-этапы0–10 и frontend11.1–11.5 сохранены.

## 2. Реализовано

- `src/Identity.Admin.Web/src/features/users/parameters.ts`: типизированная модель URL,
  канонические query params, defaults/whitelist, search200/control chars, pageSize1–100,
  int32 Page и offset. Transport использует фактические PascalCase параметры OpenAPI.
- `useUsersParameters.ts`: debounce400ms, browser back/forward, сброс page1 при поиске/
  фильтрации/сортировке/page-size; reset всех параметров. Незавершённый ввод не теряется
  при быстром выборе другого фильтра, но отменяется при переходе по history.
- `api.ts`, `useUsers.ts`: единственный существующий typed client, generated DTO,
  AbortSignal, query key со всеми параметрами, staleTime30s, retry=false. Числа/числовые
  строки нормализуются без округления; в cache только документированные поля.
- `UsersPage.tsx`, `UsersTable.tsx`: Ant Design Table Email/Display name/Status/
  Email verified/Created/local time/Details; серверные sort/page, total, filters и
  Refresh. Loading, настоящий empty/0, safe error/manual GET retry; stale таблица
  скрывается после failed refresh. Пустая страница>1 предлагает Go to first page.
- `components/LocalDateTime.tsx`: локальное en-US время и UTC ISO title. StatusTag и
  остальные общие состояния/тема переиспользованы; HTML выводится как текст.
- Узкие экраны: focusable horizontal scroll region, flex filters/footer, page size
  доступен и на360px. Empty state вне широкой таблицы. Короткие Select virtual=false;
  клавиатура и все опции доступны. На1280px все колонки помещаются.
- `routes.tsx`, Navigation: защищённый lazy `/admin/users`, активный пункт Users;
  Audit остаётся disabled. View details — обычный SPA Link, state.usersSearch
  сохраняет фильтры. Сам details screen относится к11.7 и сейчас не реализован.
- AuthGate/returnPath: только allowlisted параметры `/admin/users` сохраняются после
  login; прежние запреты absolute URL/login/fragment/backslash сохранены.
- `parameters.test.ts`, `users.test.tsx`:26 новых unit/component tests. Старые shell/
  auth tests адаптированы к включённому Users; expired-session fake fetch создаёт
  новый Response при каждом вызове, исключая повторное чтение одного body.
- `e2e/users.spec.ts`:8 реальных browser scenarios для обеих ролей, pagination/search/
  filters/history/empty, keyboard scroll/select, manual GET retry, safe login return
  и360/768/1280 snapshots. Shell spec проверяет актуальное меню и будущий details404.
- Vite vendor groups дополнены Ant Design Table без повышения500kB warning limit.
  Playwright workers=4, retries=0 для ограничения локальной browser/socket нагрузки.
- Root/frontend README, CHANGELOG, docs/authentication/shell и новый docs/users.md.
  Backend/API/OpenAPI schema/DB/миграции/solution/security policies/CI/Docker/env samples
  не менялись. Новых dependencies и конфигурационных переменных нет.

## 3. Решения

URL — источник committed состояния; отдельно только поисковый черновик. Нет нового
global state, client-side сортировки/пагинации, загрузки всех users для поиска или
fixtures в production. Запросы проецируют существующий контракт. Обе роли читают
одинаковый каталог; авторизация остаётся на сервере. Изменения поиска/фильтров дают
новую запись history, исправление невалидного URL — replace. Query cancellation и
очистка данных при завершении admin session сохранены.

Граница ТЗ:11.6 требует ссылку на details, а экран задан11.7. Поэтому ссылка ведёт
на будущий `/admin/users/:id` и существующий protected NotFound, без detail API или
заглушки карточки. Это явно описано в runbook; сам11.7 не начинался.
Vendor split задаёт cache boundaries, не обещает уменьшение всего initial download:
общие vendor chunks могут загружаться вместе с shell.

## 4. Проверки

### Commands run

Из `src/Identity.Admin.Web`:

```powershell
npm run format
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run test:run -- src/features/users
npm run build
npm run api:check
npm run test:e2e
```

Последние три команды выполнены внутри HTTPS smoke script (build также запускает
typecheck). Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage11-6'
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- Итоговые typecheck/lint/Prettier exit0. **138 frontend tests passed**,0failed/skipped,
  10files (112 прежних+26 новых). Проверены все query parameters, границы int32/offset,
  debounce/focus/history, быстрый выбор фильтра при pending search, page-size reset,
  пустой ответ, failure/retry/refresh, cancellation, null/date и sensitive projection.
- Итоговый HTTPS smoke exit0: **21 Playwright Chromium scenarios passed**,0retry,
  4workers,17.8s. Реальные PostgreSQL17/Mailpit/API/CLI и Vite proxy с TLS validation;
  все6 существующих миграций применены, OpenAPI types match, тестовых секретов в dist нет.
  Fixture processes/Compose/volumes/keys очищены скриптом. Снимки360/768/1280 просмотрены.
- Production build exit0, без предупреждения о500kB chunk. UsersPage7.30kB(gzip2.87),
  app104.01kB(gzip34.44), Antd207.82,Table328.48,UI primitives310.81,React218.76,
  Dashboard357.34kB; max357.34kB. CSS5.46+2.98kB. Общие vendor chunks могут preload.
- Backend restore/build exit0, **0warnings/0errors**. **470 backend tests passed**,
  0failed/skipped: Unit147/API45/Integration278, настоящие PostgreSQL/Mailpit в Docker.
  TRX stage11-6 сохранены в ignored TestResults; integration1m30s.
- EF: No changes have been made to the model since the last migration. .NET format,
  secret guard и diff whitespace check exit0. Установка npm не требовалась: dependencies
  и lockfile не менялись. API Docker image заново не собирался (backend не менялся).

Промежуточные сбои не засчитаны как успешные проверки: sandbox spawn EPERM исправлен
запуском Vitest с разрешением; устранены неверные RTL options/селекторы, отсутствие
ResizeObserver в jsdom (локальный test stub), race ожидания новой страницы и reuse
Response в старом auth test. History test выявил и помог исправить возврат старого
черновика. Browser negative test теперь удерживает network abort до ручного retry
(StrictMode повторно монтирует GET); status filter проверяется настоящей клавиатурой,
вместо нестабильного click-selector для popup. Один прогон16workers завершился
ERR_NO_BUFFER_SPACE на открытии login: parallelism ограничен4 без добавления retries.
После правок выполнен успешный полный прогон. NO_COLOR/FORCE_COLOR warnings runner
косметические. Первое чтение __EFMigrationsHistory на пустой DB ожидаемо предшествует
созданию таблицы; все миграции затем применены успешно.

## 5. Безопасность

Read-only UI не отправляет user commands. Cookie/CSRF/401/403 и server authorization
сохранены. Нет credential persistence, raw backend details, HTML injection или
неизвестных response fields в cache. Page/filter/sort allowlist, safe redirect и
AbortSignal ограничивают некорректные запросы и поздние ответы. Поиск по email/name
присутствует в URL/history по требованиям ТЗ; никаких analytics/browser storage.
Секреты browser smoke только disposable fixture, не репозиторий/bundle/screenshots.

## 6. Ограничения / Known issues

- Карточка11.7, invite/actions/audit/account pages и production release остаются
  последующими подэтапами. Links details до11.7 показывают protected NotFound.
- total ограничен Number.MAX_SAFE_INTEGER для точного отображения, переходы —
  поддерживаемым backend int32 Page/offset; вне допустимого числового DTO — явная ошибка.
- Firefox/WebKit, axe/Lighthouse и production deployment не проверялись. Нет новой
  frontend Docker/CI/proxy/CSP инфраструктуры (этап12), сетевые ограничения до MFA прежние.
- Известных блокеров11.6 нет. Обновлённые env samples не нужны: новых настроек нет.

## 7. Следующий этап

**11.7 — Карточка пользователя: только чтение**, только после отдельного запроса.
Не начат. Commit/push/deploy не выполнялись.

---

# История: этап 11.5 завершён

**Completed: 11.5**

**Next: 11.6 — только по отдельному запросу пользователя.**

## 1. Этап

**11.5 — Dashboard со статистикой**, 2026-10-19.
Изучены AGENTS.md, основной Spec, дополнение v1.2, status, Git и реальные dashboard
DTO/query/repository. Начальное дерево чистое;11.4 — commit **af8180d**.
Реализован только11.5; Users/Audit/actions остаются следующими подэтапами.

## 2. Реализовано

- `src/Identity.Admin.Web/src/features/dashboard/api.ts`: typed GET через существующий
  API client, DTO из generated OpenAPI, безопасная проекция/нормализация number|string.
  Невалидные, отрицательные, дробные и неточные counts отклоняются, а не заменяются0.
- `useDashboard.ts`: существующий queryKeys.dashboard(), AbortSignal, staleTime30s,
  networkMode always и прежний retry=false. Ручной Refresh GET, без interval polling.
- `DashboardPage.tsx`, `DashboardMetrics.tsx`: семь настоящих счётчиков — Total/Active/
  Pending verification/Invited/Disabled/Deactivated/Active sessions; время снимка
  локальное, UTC ISO tooltip. Active sessions явно исключает admin-cookie sessions.
- `RegistrationChart.tsx`, `format.ts`: Recharts line chart с30 UTC daily values,
  подписями дат, integer Y axis, tooltip с датой/единицами, периодом From inclusive /
  To exclusive и пояснением partial today. Intl en-US согласован с текущим app locale;
  DateOnly остаётся UTC днём. Доступная native details/table с30 строками и keyboard
  exploration через accessibilityLayer, без анимации линии.
- Loading, настоящие0/empty period, отдельно отсутствующий ряд, safe errors/manual
  GET retry. При refresh остаётся прежний snapshot с asOf и pending; при failure
  stale карточки скрываются. Нет fallback counters/fixtures в production.
- Routes: `/admin` Dashboard вместо удалённого пустого ConsolePage. React.lazy/Suspense
  отделяет dashboard/Recharts от login chunk; прежние guard/error boundary сохранены.
  Пункт меню переименован в Dashboard; Users/Audit disabled. Ant Design Statistic
  token и общая responsive CSS; новых page color/font overrides/UI kits нет.
- `package.json`/lockfile: точный **recharts3.10.1**, проверенный по official release;
  зависимости установлены без force/legacy peers. Другие прямые версии прежние.
- `dashboard.test.tsx`, shared test/dashboard fixture; существующие auth/shell tests
  адаптированы под dashboard, feature transport mock используется только в старых
  изолированных auth/layout tests. Новые dashboard tests используют настоящий client
  и Recharts, fake fetch только на network boundary.
- `e2e/dashboard.spec.ts`: реальные метрики для двух ролей, keyboard tooltip/table,
  actual registration через public API и изменение Total/Pending после Refresh.
  Три dashboard-сценария serial, остальные smoke parallel; одна disposable DB на run.
  Shell checks разрешают только auth/dashboard reads, ждут график/ResizeObserver
  после sidebar transition, проверяют overflow360/768/1280 и сохраняют snapshots.
- README/CHANGELOG/frontend README, docs/authentication/shell и новый docs/dashboard.md.
  IMPLEMENTATION_STATUS обновлён. Backend/CQRS/API/OpenAPI schema/DB/миграции/solution/
  production Docker/CI/security policies/env samples и версии продукта сохранены.

## 3. Решения

Данные берутся из готового snapshot DTO, не из users list. Суммы/UTC buckets не
пересчитываются на клиенте; нет фиктивных процентов роста или новой аналитики.
Период chart явно UTC, время asOf локальное; последний серверный bucket частичный.
Active sessions — native/BFF, не число вошедших администраторов. Обе admin роли читают
одинаковый экран; сервер остаётся источником authorization, существующий401/403 flow
не дублируется. Sensitive transport/session cache cleanup этапа11.4 сохранён.

Recharts — единственная новая прямая библиотека, предусмотрена11.5. Использован обычный
React lazy route, без новой системы загрузчиков/глобального state. Карточки/график/API/
Query отделены по ответственности. Нет дополнительных hooks/abstractions без кейса.
ResponsiveContainer initialDimension300x280 — стартовая geometry, не фиктивные данные;
при реальном layout размер меняется через ResizeObserver. В jsdom реальных размеров нет,
поэтому responsive/keyboard/tooltip проверяются настоящим Chromium.

## 4. Проверки

### Commands run

Из frontend:

```powershell
npm install --save-exact recharts@3.10.1
npm ci
npm run format
npm run typecheck
npm run lint
npm run format:check
npm run test -- --run
npm run build
npm run api:check
npm run test:e2e
```

Последние две команды также выполнены внутри существующего HTTPS smoke script.
Из корня:

```powershell
dotnet restore Identity.slnx
dotnet build Identity.slnx --configuration Release --no-restore
dotnet test Identity.slnx --configuration Release --no-build --no-restore --logger 'trx;LogFilePrefix=stage11-5'
dotnet tool restore
$env:Database__ConnectionString = 'Host=localhost;Database=identity_design;Username=identity'
dotnet ef migrations has-pending-model-changes --project src/Identity.Infrastructure --configuration Release --no-build
dotnet format Identity.slnx --no-restore --verify-no-changes
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
pwsh -NoProfile -File scripts/Test-RepositorySecrets.ps1 -SelfTest -IncludeUntracked
git -c core.safecrlf=false diff --check
```

### Observed results

- npm install и отдельный успешный npm ci:371 audited packages,0vulnerabilities.
  TypeScript/lint/Prettier/build exit0. **112 frontend tests passed**,0failed/skipped,
  8files:94 прежних +18 dashboard. Проверены7 метрик,0users/empty/no-series, safe errors,
  manual GET retry/refetch/pending/failure, cancellation, string counts, invalid values/
  dates, projection, period end-exclusive, local snapshot/UTC title и таблица30 значений.
- **13 Playwright Chromium scenarios passed**,0retry:10 прежних auth/shell и3 dashboard.
  Реальные PostgreSQL17/Mailpit/HTTPS API/Vite proxy, REST verification/CLI roles;
  Administrator и ReadOnly читают все7 фактических метрик, keyboard tooltip/table,
  public registration увеличивает Total/Pending после refresh. Cookies/storage guards
  прежние. На360/768/1280 нет overflow после загрузки chart/sidebar ResizeObserver.
- Production build с отдельным DashboardPage358.75kB/gzip104.68; app101.18kB,
  React218.76kB, Antd412.88kB, UI primitives182.01kB. Chunk warning отсутствует,
  warning threshold не повышался. Final build проверен повторно внутри HTTPS smoke.
- OpenAPI types match Identity.Api; model pending changes отсутствуют. Smoke применяет
  шесть существующих миграций к пустой DB; новых миграций нет. Проверка dist на временные
  password/DB/PFX secrets проходит; fixture containers/volumes/keys/processes очищены.
- .NET restore/build/format exit0:0warnings/0errors. **470 backend tests passed**,
  0failed/skipped — Unit147 + API45 + Integration278 с реальными PostgreSQL/Mailpit.
  TRX stage11-5 сохранены в ignored test outputs.
- Все3 размера dashboard визуально просмотрены. Снимки — ignored test-results.
  Secret guard/self-tests и git diff --check проходят.

Промежуточные проблемы исправлены: первая npm install была недоступна в sandbox и
повторена с разрешённым доступом. Ошибочно параллельно запущенные npm ci и frontend
checks конфликтовали в node_modules (ENOTEMPTY/missing package); после их завершения
последовательный npm ci и все проверки успешны. Исправлен неверный ByRole exact option.
Первый desktop screenshot мог захватить промежуточную ширину chart при sidebar resize;
smoke теперь ждёт измерение и проверяет viewport после data load. Последующие PNG
имеют точные360/768/1280px. NO_COLOR/FORCE_COLOR warning runner косметический.

## 5. Безопасность

Только GET read screen и ручной GET refresh. Нет account actions/новых прав или server
policy changes. Query key не содержит secrets; fetch same-origin/no-store/redirect:error,
signal до network. Unknown response fields не кешируются. Невалидные значения не
превращаются в ложные метрики. Password/tokens/cookies не передаются chart/query,
storage и console; прежние session/CSRF/401/403 механизмы сохранены.

## 6. Ограничения / Known issues

Блокеров11.5 нет. Dashboard отображает safe integers до Number.MAX_SAFE_INTEGER;
большие int64 отклоняются явно, не округляются молча. Browser smoke Chromium/dev HTTPS;
production bundle собран отдельно. Production frontend hosting/proxy/Docker/CSP/CI,
axe/Lighthouse и другие браузеры не проверялись. Отдельный API Docker image smoke
не повторялся: image/backend не менялись; настоящие Docker integration и HTTPS API/UI
проверки выполнены. Users/search/detail/actions/audit/account pages — следующие11.x.
Существующее требование restricted admin network до MFA остаётся.

## 7. Следующий этап

**11.6 — Пользователи: таблица и поиск**, только по отдельному запросу. Не начат.

---

# Предыдущий отчёт: этап 11.4
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
