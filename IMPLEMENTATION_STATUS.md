# Текущий статус: этап 11.8 завершён

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
