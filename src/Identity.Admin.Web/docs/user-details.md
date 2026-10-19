# User details (11.7)

Защищённый lazy route `/admin/users/:userId` доступен обеим административным ролям.
Существующий Users navigation остаётся выбранным. Нет новых API, миграций, зависимостей
или env variables. Общая тема/компоненты и архитектура cookie/CSRF сохраняются.

## Данные и границы

`features/users/detailsApi.ts` использует generated OpenAPI и общий typed API client:

| GET                                                             | Поля экрана                                                                                                     |
| --------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `/api/v1/admin/users/{userId}`                                  | id, email, displayName, status, emailVerified, emailVerifiedAt, createdAt, updatedAt, disabledAt, deactivatedAt |
| `/api/v1/admin/audit-events?Page=1&PageSize=10&UserId={userId}` | id, eventType, occurredAt                                                                                       |

У audit UserId означает legacy user_id **или** target_user_id. Это события об аккаунте,
не отдельная выборка действий администратора как actor. Backend сортирует occurredAt/id
DESC; UI сохраняет порядок и читает только первую страницу10, без загрузки всего журнала,
client-side фильтрации или нового экрана аудита. Действует существующая retention policy;
отсутствие событий не означает отсутствие действий за всю историю. Полный журнал —11.12.

API layer проверяет поля/даты, соответствие ID запрошенному пользователю и принадлежность
audit rows. В cache попадают только перечисленные поля. Actor/target/correlation/clientId,
лишние detail properties, любые unexpected metadata/credential fields не копируются.
Поля password/hash/token/outbox отсутствуют в UI. Server authorization остаётся источником
доступа, UI читает данные исключительно через same-origin GET с AbortSignal.

Query hooks `useUserDetails`/`useUserActivity` используют прежние factories:
users.detail(userId), audit.list({Page:1,PageSize:10,UserId}). staleTime30s, retry=false,
networkMode=always. Audit монтируется после успешного detail GET. Account404/invalid UUID
не запускают audit. Невалидный или нулевой UUID даёт отдельное состояние до HTTP запроса;
неизвестный валидный ID — User not found. Ошибка активности не скрывает профиль; GET retry
повторяет только соответствующий запрос. Неудачный refresh скрывает устаревшие данные
своей секции. Глобальные401/403 обрабатываются существующим AuthProvider.

## Интерфейс и навигация

Ant Design Descriptions: Email, Display name, StatusTag, User ID, verification, Created,
Last updated и lifecycle dates. Nullable поля имеют явные значения Not provided /
Not verified / Not disabled / Not deactivated. Даты локальные en-US, title содержит UTC ISO.
ID доступен как текст и через keyboard-friendly Copy user ID. Clipboard API вызывается
только по нажатию, без DOM fallback; success/failure сообщается через live status. При
отказе clipboard можно выделить видимый UUID. Не копируется весь профиль или URL.

Ссылки из таблицы переносят **те же allowlisted filters** в query карточки, например:
`/admin/users/{uuid}?page=2&pageSize=50&search=alice&status=Active&sortBy=email&sortDirection=asc`.
Breadcrumb Users и Back to users строят явный `/admin/users` с этими параметрами.
Reload/direct load/login сохраняют возврат без router state или browser storage.
Невалидные/неизвестные параметры удаляются replace-канонизацией как в каталоге.
Absolute return URLs не принимаются. Поиск по email/name остаётся в URL/history согласно ТЗ.

На360px Descriptions в одну колонку, ID/тексты переносятся. Заголовок активности и Refresh
не конкурируют за ширину одной строки. Проверяются360/768/1280px и keyboard copy.
Нет placeholders/buttons для invitation, password reset, enable/disable, редактирования
или session revoke. Сессии — этап11.11; следующие write flows начинаются с11.8.

## Проверки

```powershell
npm run typecheck
npm run lint
npm run format:check
npm run test:run
npm run build
```

Из корня `pwsh -NoProfile -File scripts/Test-AdminWebSmoke.ps1` проверяет реальные HTTPS
API/Vite/PostgreSQL/Mailpit, OpenAPI drift и production build. `e2e/userDetails.spec.ts`
покрывает обе роли, переход без document reload, реальные profile/activity, Clipboard API,
reload/back с фильтрами, invalid ID/404, независимый activity failure/manual retry и
360/768/1280 screenshots/отсутствие page overflow/browser storage. Тестовые permissions
clipboard применяются только в disposable browser context.

`userDetails.test.tsx` проверяет null fields/все5 статусов, loading/404/retry, transport,
ограничение и фильтр audit, отмену detail/activity, safe return, escaping и отсутствие
чувствительных полей в DOM/cache, clipboard denial и malformed/wrong-user DTO.
`src/test/userDetails.ts` содержит только тестовые fixtures, не production fallback.
