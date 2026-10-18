# Безопасность Identity — этапы 0–2

Схема БД хранит credentials отдельно от профиля и только token_hash для токенов.
Реализованы публичные registration/verification операции. Login/JWT/refresh ещё отсутствуют.
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
изменений. Это не заменяет будущие use-case транзакции и обработку replay.

Outbox допускает только типизированный AES-GCM envelope (keyId, nonce, ciphertext,
tag), хранит его в jsonb с ограничениями формы и очищает payload после MarkProcessed.
AES-256-GCM реализован через System.Security.Cryptography.AesGcm с 32-byte ключом,
12-byte случайным nonce и 16-byte tag; AAD связывает envelope с Identity/outbox version/key ID.
Ключи читаются из отдельных файлов вне БД/репозитория, старые key ID поддерживаются
до отправки backlog. Исходные массивы plaintext и key buffers очищаются по окончании использования.
Audit metadata ограничен числовым attemptCount; произвольные
email/IP/password/token поля не принимаются. Retention worker появится позже.

Пароли обрабатываются Microsoft PasswordHasher (PBKDF2, IdentityV3, 600000 iterations
по умолчанию, минимум 600000). NFKC применяется одинаково в validation/hash/verify;
пробелы сохраняются. Политика считает Unicode code points: 15–128, до 1024 UTF-8 bytes
до нормализации/512 после; invalid Unicode отклоняется. Локальный denylist содержит
распространённые пароли, включая длинные варианты и известные демонстрационные фразы;
это начальный список, не полная база всех утечек. Rehash-needed корректно возвращается;
обновление хеша при login будет реализовано на этапе 3.

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

Это ещё не готовый authentication service. HTTPS/reverse proxy, bearer authentication,
полная защита business endpoints и security-hardening будут реализованы в соответствующих этапах.
Не публикуйте текущее dev-окружение в Интернет.

Будущие ограничения продукта по ТЗ:

- REST token protocol не является OAuth 2.0/OIDC и не предоставляет стандартный SSO.
- `clientId` — публичный селектор audience, а не доказательство подлинности клиента.
- Браузер использует BFF; refresh token нельзя хранить в localStorage/sessionStorage
  или JavaScript-accessible cookie.
- Отзыв сессии не прекращает действие уже выданного JWT в стороннем API до `exp`.
- SMTP требует transactional outbox с AES-256-GCM для чувствительной части payload;
  ключи хранятся вне БД и репозитория. Этап 2 реализует sender/worker; JWT keys появятся отдельно.
