# Безопасность Identity — этапы 0–4

Refresh хранит полную consumed/replaced chain. Row locks user/session/token и fresh
чтение после блокировки предотвращают двойную rotation одного token. Replay любого
consumed token, включая уже expired, отзывает session и все refresh строки, включая
победителя конкурентного запроса. Revoke/audit commit предшествует 401; сбой commit
возвращает 500 без ложного обещания завершённого отзыва. Grace window нет.

ClientId/audience берутся только из session binding. Подмена полей JSON их не меняет,
неактивный/locked пользователь, disabled client, changed audience и legacy unbound
session не получают новых токенов. Raw refresh ни в БД, ни в audit, ни в логах.
Login/refresh response — обычный class с безопасным ToString и Cache-Control no-store.

Logout public и идемпотентен; logout-all/revoke требуют Bearer и повторно проверяют
текущую session/user под row locks. Владелец берётся из sub, никогда из JSON/query.
Чужой/неизвестный UUID даёт одинаковый 404. Свои сессии доступны между включёнными
клиентами одного пользователя без business roles. Отзыв текущей сессии немедленно
блокирует её JWT на Identity; сторонний API видит отзыв только по exp (+skew).
Ручной revoke пишет event_type=logout и reason UserRevoked в session; отдельного
session_revoked audit type спецификация не определяет.

Cleanup хранит всё семейство до absolute expiry + 1 день (настраивается), даже после
logout. Истечение отдельного refresh не является основанием удаления replay evidence.
Потеря ответа после commit rotation требует нового login; повтор старого токена
обрабатывается как replay. Клиентам нужна сериализация refresh и защищённое хранилище.

Login сериализует попытки одного аккаунта через PostgreSQL FOR UPDATE. После 5 неверных
паролей аккаунт закрыт на 15 минут; обращения во время блокировки её не продлевают.
После истечения начинается новый счётчик; успешный login сбрасывает ошибки и выполняет
rehash при необходимости. Unknown/неактивный/locked пользователь и неизвестный клиент
получают одинаковую 401. Unknown account выполняет PBKDF2 на случайном dummy hash;
неактивные/locked аккаунты проверяют реальный hash. Это уменьшает timing shortcuts,
но не обещает идентичную сетевую латентность для разных DB/cache/старых hash parameters.
IP/account rate limits общие с регистрацией/resend и локальны экземпляру; DB lockout
согласован между replicas. Сырой email/IP не попадает в audit/metric labels.

Session + refresh hash + login_success audit + rehash/counter reset фиксируются одной
транзакцией. Отказ до commit откатывает всё; ответ с токенами возвращается только
после commit. Refresh — RNG 32 bytes; в PostgreSQL только SHA-256. Отключённый клиент
не получает токен; clientId не является секретом или аутентификацией клиента.

Access JWT: Microsoft IdentityModel RS256, RSA >=2048, default lifetime 900 секунд,
только обязательные claims без email/ролей. JWT private PEM из независимого mount;
JWKS содержит только public n/e и kid/kty/use/alg. Строго проверяются подпись, kid,
RS256, issuer, audience, exp/nbf, token_use и обязательные claims. Все MVC Controllers
защищены глобальным AuthorizeFilter, исключения — явные AllowAnonymous actions.
Identity дополнительно читает активность user/session из БД; внешние API проверяют
JWT локально через JWKS и только свою audience. Ни OIDC discovery, ни Authority нет.
При закрытии аккаунта/сессии ранее выданный JWT во внешнем API живёт до exp (+skew).
Ротация ключей и предварительная публикация public key описаны в deployment docs.

Тесты проверяют wrong issuer/audience/alg/kid/signature, unsigned/expired/future tokens,
token_use, disabled/deactivated user, отсутствующую/отозванную/истёкшую session,
restart/rotation и отсутствие raw password/access/refresh/PEM/email в собранных логах.
Production отвергает test-/dev- key IDs при включённом JWT. Отсутствует готовый
test/private key в исходниках; ключи тестов случайные и удаляются после завершения.

Схема БД хранит credentials отдельно от профиля и только token_hash для токенов.
Реализованы registration/verification/login, JWT/JWKS и начальный refresh token.
Refresh rotation/replay/logout и управление собственными сессиями реализованы.
Секреты в репозиторий не включены; `.env` и файлы ключей исключены из Git, а Docker
получает только исходники и файлы сборки через allowlist контекста.
Локальный пароль PostgreSQL обязателен и задаётся пользователем вне исходников.

OpenAPI публикуется только в Development. API использует стандартные ProblemDetails;
ответ на исключение не раскрывает stack trace/exception message. Обработчик пишет
только тип неожиданного исключения и trace ID; автоматические diagnostics для
обработанных исключений отключены. CORS не включён.
Compose публикует порты только на loopback, API работает под непривилегированным
пользователем. Mailpit предназначен исключительно для локальной разработки.
Логи HTTP request/response body и секретов не подключены.

Database options обязательны; Include Error Detail и Log Parameters в connection
string запрещены startup validation. EF sensitive-data logging не включён.
Уникальность normalized_email/token_hash, FK и check constraints применяются в БД.
Для изменяемых строк xmin предотвращает незаметное перезаписывание конкурентных
изменений. Refresh/revoke дополнительно используют явные транзакции и row locks.

Outbox допускает только типизированный AES-GCM envelope (keyId, nonce, ciphertext,
tag), хранит его в jsonb с ограничениями формы и очищает payload после MarkProcessed.
AES-256-GCM реализован через System.Security.Cryptography.AesGcm с 32-byte ключом,
12-byte случайным nonce и 16-byte tag; AAD связывает envelope с Identity/outbox version/key ID.
Ключи читаются из отдельных файлов вне БД/репозитория, старые key ID поддерживаются
до отправки backlog. Исходные массивы plaintext и key buffers очищаются по окончании использования.
Audit metadata ограничен числовым attemptCount; произвольные
email/IP/password/token поля не принимаются. Retention audit/outbox относится к следующим этапам;
cleanup refresh-семейств реализован на этапе 4.

Пароли обрабатываются Microsoft PasswordHasher (PBKDF2, IdentityV3, 600000 iterations
по умолчанию, минимум 600000). NFKC применяется одинаково в validation/hash/verify;
пробелы сохраняются. Политика считает Unicode code points: 15–128, до 1024 UTF-8 bytes
до нормализации/512 после; invalid Unicode отклоняется. Локальный denylist содержит
распространённые пароли, включая длинные варианты и известные демонстрационные фразы;
это начальный список, не полная база всех утечек. Rehash-needed корректно возвращается;
успешный login обновляет такой хеш, сохраняя дату фактической смены пароля.

Registration всегда выполняет password hash и построение encrypted email, в том числе
при дубле email. API возвращает одинаковый 202, уникальность гарантирует PostgreSQL.
Resend для неизвестного/неактивного пользователя не отправляет письмо и даёт тот же 202.
Полное постоянство времени ответа не гарантируется: DB/SMTP scheduler и workload
могут влиять на latency; полная timing-hardening матрица относится к этапу 6.
IP/account rate limits локальны процессу, обнуляются при restart и не суммируются между
репликами; на публичном proxy также нужны согласованные лимиты. Счётчики email используют
SHA-256 от нормализованного адреса, без email в логах/metric labels. Forwarded headers
не доверяются автоматически. Auth body ограничен 16 KiB, oversized request получает 413.

Register и verify сохраняют свои состояния с audit events в одной транзакции.
Для verify/resend используется user row lock; разные токены одного user не могут
успешно активировать его дважды. Raw one-time token находится только в памяти и письме;
в таблице token hash, в outbox зашифрованы recipient/subject/body целиком.
SMTP protocol logging отсутствует. Message-Id стабилен, но delivery остаётся at-least-once.

Полная версия продукта ещё не завершена. HTTPS/reverse proxy hardening, refresh lifecycle,
profile/password lifecycle и дополнительные security checks относятся к последующим этапам.
Не публикуйте текущее dev-окружение в Интернет.

Будущие ограничения продукта по ТЗ:

- REST token protocol не является OAuth 2.0/OIDC и не предоставляет стандартный SSO.
- `clientId` — публичный селектор audience, а не доказательство подлинности клиента.
- Браузер использует BFF; refresh token нельзя хранить в localStorage/sessionStorage
  или JavaScript-accessible cookie.
- Отзыв сессии не прекращает действие уже выданного JWT в стороннем API до `exp`.
- SMTP требует transactional outbox с AES-256-GCM для чувствительной части payload;
  ключи хранятся вне БД и репозитория. AES и RSA JWT keys независимы.
