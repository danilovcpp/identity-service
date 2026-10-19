# Identity Console: Dashboard (11.5)

`/admin` — read-only обзор с реальным `GET /api/v1/admin/dashboard`.
Administrator и ReadOnly читают один и тот же экран; существующий AuthGate остаётся
границей доступа. Users/Audit/actions в этом этапе не включены.

## Данные и представление

Feature находится в `src/features/dashboard`: `api.ts` получает/нормализует DTO,
`useDashboard.ts` управляет Query, DashboardPage связывает states, DashboardMetrics
и RegistrationChart отвечают за представление. Тип результата выведен из generated
OpenAPI; backend-контракт не расширяется. ConsolePage с пустым workspace удалён.

Семь метрик: Total users, Active users, Pending verification, Invited, Disabled,
Deactivated, Active sessions. Счётчики получены с сервера и не рассчитываются по
загруженным пользователям. Total охватывает все сохранённые аккаунты; activeSessions
считает только native/BFF session families, **не admin-cookie**. Это пояснено в UI.
`asOf` показан в локальном времени браузера, UTC ISO доступен в title элемента time.

Дневной ряд — серверные30 UTC calendar buckets, включая сегодня. `registrationTrendFrom`
включён, `registrationTrendTo` исключён; подпись отображает последний включённый день.
Сегодня неполный, по asOf. DateOnly не преобразуется в локальную дату: иначе западные
timezone сдвигали бы подписи на предыдущий день. Числа/даты форматируются Intl en-US,
согласованно с текущим Ant Design locale. Суммы не подменяются фиктивным процентом роста.

Recharts **3.10.1** закреплён в package/lockfile. Используются LineChart,
ResponsiveContainer, UTC ticks и tooltip; цвета/типографика берутся из Ant theme tokens.
Анимация линии выключена; accessibilityLayer и стрелки клавиатуры включены.
Native details «View daily values» открывает доступную таблицу всех30 значений.
Нет второго UI kit. Новые dashboard styles — geometry/focus в общей CSS.
React.lazy/Suspense отделяет dashboard/chart от публичного login bundle; сборка
по-прежнему проверяет весь код. Ошибка загрузки chunk попадает в общий error boundary.

Официальные источники: [Recharts release](https://github.com/recharts/recharts/releases/tag/v3.10.1),
[ResponsiveContainer](https://recharts.github.io/api/ResponsiveContainer/),
[accessibility](https://github.com/recharts/recharts/blob/main/storybook/stories/API/Accessibility.mdx).

## Состояния и обновление

- Первый запрос — LoadingState, без нулевых fallback карточек.
- Нулевые реальные counts показываются как0; отсутствие пользователей и нулевой ряд
  имеют отдельные сообщения. Пустой массив ряда означает data unavailable, а не
  синтезированные30 нулей. Производственных fixtures нет.
- Query key `queryKeys.dashboard()`, staleTime30s, retry=false (существующий default).
  Refresh принудительно делает GET; во время запроса кнопка disabled/loading,
  прежний snapshot остаётся с его asOf и явным статусом refreshing.
- Ошибка первого GET или refresh — ErrorState с ручным GET retry; старые числа не
  показываются как актуальный успешный ответ. Network mode always даёт явную ошибку
  offline fetch, не бесконечный queued loading. Нет interval polling.
- Signal передаётся до fetch; уход со страницы/смена личности отменяет read.
  401/403 обрабатываются текущим владельцем сессии. Query cache не persist-ится.
- `number | string` счётчики OpenAPI преобразуются в неотрицательные safe integers.
  Null/boolean/NaN/fraction/negative/unsafe integers отклоняются как invalid response;
  интерфейс не округляет большие int64 молча. Граница отображения — Number.MAX_SAFE_INTEGER.
  Невалидные даты также не превращаются в произвольные подписи.

## Проверки

`npm ci`, typecheck/lint/format:check, `npm run test -- --run`, `npm run build`.
Unit tests рендерят настоящий Recharts, без моков production-компонентов; jsdom не имеет
layout/ResizeObserver, поэтому используется его initialDimension300x280. Настоящее
изменение размеров/tooltip/keyboard проверяет Chromium.

Из корня после Release build:

```powershell
pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1
```

Существующая [HTTPS fixture](authentication.md) проверяет OpenAPI/build и13 сценариев:
login/logout/guards и shell360/768/1280, чтение всех семи метрик обеими ролями,
keyboard tooltip и30 table rows. Последний serial dashboard-сценарий регистрирует
уникального пользователя через реальный public API и проверяет Total/Pending после
Refresh. Эти записи удаляются вместе с disposable PostgreSQL в конце прогона.
Shell smoke разрешает только auth/dashboard reads; остальных бизнес-запросов нет.
После collapse/expand ждёт ResizeObserver и проверяет ширину загруженного chart,
а не только первоначальный экран загрузки. Screenshots в ignored test-results.

Production Docker/proxy/CSP/CI, axe/Lighthouse и остальные11.x — отдельные этапы.
