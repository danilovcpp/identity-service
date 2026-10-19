# Users catalog (11.6)

`/admin/users` доступен Administrator и ReadOnly через прежний AuthGate. Читает только
`GET /api/v1/admin/users`, реальные данные из PostgreSQL. Навигация Users включена;
приглашения, изменения и массовые действия не реализованы. Карточка доступна с [11.7](user-details.md).

## Контракт и URL

DTO берётся из generated OpenAPI. Строка содержит только id, email, displayName, status,
emailVerified и createdAt; null displayName показан как Not provided. Дополнительные
поля ответа не попадают в Query cache. Числовые строки page/pageSize/total нормализуются;
невалидные или неточные integer значения отклоняются с безопасной ошибкой.

Frontend использует camelCase URL, transport — фактические PascalCase имена OpenAPI:

| URL           | API           | Правило                                                            |
| ------------- | ------------- | ------------------------------------------------------------------ |
| page          | Page          | 1-based, default 1; offset `(page - 1) * pageSize <= int.MaxValue` |
| pageSize      | PageSize      | default 25, от 1 до 100                                            |
| search        | Search        | email/name, trim, без control chars, до 200 UTF-16 code units      |
| status        | Status        | PendingVerification, Active, Invited, Disabled, Deactivated        |
| emailVerified | EmailVerified | true / false; отсутствие — все                                     |
| sortBy        | SortBy        | createdAt / email / status; default createdAt                      |
| sortDirection | SortDirection | asc / desc; default desc                                           |

Невалидные параметры получают defaults; unknown/duplicate/default параметры удаляются
канонизацией с replace, без дополнительной записи истории. Неизвестные sort/status
не отправляются серверу. URL state — единственный источник committed фильтров/страниц;
локально хранится только черновик поиска с debounce 400ms. Завершённые изменения создают
записи history. Back/forward отменяет незавершённый поиск и восстанавливает URL/input.
Поиск, status, verification, sort и page size переводят на page 1. Reset filters
сбрасывает также sort/page size. Запросы используют все параметры в query key и AbortSignal;
Если другой фильтр выбран до окончания debounce, он сохраняет текущий поисковый ввод.
при смене query прежняя страница не выдаётся за новую. Никаких client-side sort/page.

Пример: `/admin/users?page=2&pageSize=50&search=alice&status=Active&emailVerified=true&sortBy=email&sortDirection=asc`.
Только allowlist этих параметров сохраняется в return path после входа. Arbitrary query,
absolute redirects и fragment не принимаются. Поиск по email/name виден в URL и history
по требованиям ТЗ; не вводите пароли/токены в поле поиска. Нет analytics/storage persistence.

## UX и границы

Ant Design Table сортирует исключительно через backend. Все пять состояний имеют
существующий StatusTag. Даты отображаются в локальном времени, en-US; title содержит
UTC ISO. Total отражает ответ сервера. Для total выше Number.MAX_SAFE_INTEGER показана
ошибка, без округления. Пагинация ограничивает переходы серверным int32 offset; реальный
total продолжает отображаться. Пустая устаревшая страница предлагает Go to first page,
без скрытого дополнительного запроса. Zero/empty, loading, GET error/manual retry и
Refresh поддержаны; при провале refresh устаревшая таблица скрывается.

На 360/768px таблица прокручивается внутри фокусируемого region (в том числе стрелками);
filters, total, empty/error и page-size control помещаются в viewport. На 1280px видны
все колонки. Короткие filter select не виртуализированы: все опции доступны клавиатуре
и accessibility tree. Цвета/типографика берутся из общей темы, layout — общая CSS.
Users page загружается через React.lazy; существующие vendor groups дополнены группой
Ant Design Table, чтобы не увеличивать лимит предупреждений размера сборки.

View details — React Router link на `/admin/users/:id` с каноническими query-фильтрами.
С11.7 они сохраняются в URL карточки, работают после reload/login и используются для
Back to users; router state для возврата не требуется. Карточка читает detail/activity
через существующие API. С11.8 Administrator может [пригласить пользователя](invitations.md)
из каталога и повторно отправить приглашение из карточки Invited; ReadOnly только читает.
С11.9 Disable/Enable находятся [только в карточке](account-status.md), без массовых действий.
Сервер остаётся источником авторизации; прежние cookie/CSRF и обработка 401/403 сохранены.

## Проверки

```powershell
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run build
```

Из корня: `pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1` запускает настоящие
HTTPS API/Vite, PostgreSQL/Mailpit, OpenAPI drift и Playwright. Новый users.spec.ts:
обе роли, параметры GET, debounce, page/back/forward, status, empty, reset, network
failure/manual retry, возврат после login и 360/768/1280 screenshots/keyboard scroll.
Unit/RTL проверяют URL limits/roundtrip, точный transport/query key, отмену, null fields,
UTC tooltip, debounce/focus/history, page-size reset, GET retry, escaping и проекцию.
ResizeObserver stub есть только в users.test.tsx: jsdom не измеряет layout; размер и
прокрутка проверяются реальным Chromium. Нет fixtures в production или новых зависимостей.
Playwright ограничен четырьмя workers и retries=0: suite не запускает по браузеру
на половину логических CPU, сохраняя ресурсный запас локальному API и БД.
