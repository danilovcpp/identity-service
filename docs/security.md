# Безопасность Identity — этапы 0–1

Схема БД хранит credentials отдельно от профиля и только token_hash для токенов.
Пользовательские HTTP-операции и криптографические реализации ещё отсутствуют.
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
AES-256-GCM и управление ключами ещё не реализованы — это этап 2. Тестовые envelope
используют искусственные байты для проверки хранения, не являются шифрованием
или production-ключами. Audit metadata ограничен числовым attemptCount; произвольные
email/IP/password/token поля не принимаются. Retention worker появится позже.

Это ещё не готовый authentication service. HTTPS/reverse proxy, аутентификация,
закрытие business endpoints по умолчанию, rate limits, защита паролей и токенов,
запись audit events из use cases и outbox encryption будут реализованы в соответствующих этапах спецификации.
Не публикуйте текущее dev-окружение в Интернет.

Будущие ограничения продукта по ТЗ:

- REST token protocol не является OAuth 2.0/OIDC и не предоставляет стандартный SSO.
- `clientId` — публичный селектор audience, а не доказательство подлинности клиента.
- Браузер использует BFF; refresh token нельзя хранить в localStorage/sessionStorage
  или JavaScript-accessible cookie.
- Отзыв сессии не прекращает действие уже выданного JWT в стороннем API до `exp`.
- SMTP требует transactional outbox с AES-256-GCM для чувствительной части payload;
  ключи хранятся вне БД и репозитория. На этапе 1 есть модель/схема, но нет sender/worker.
