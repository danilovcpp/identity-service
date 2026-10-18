# HTTP API Identity — этапы 0–2

Бизнес-операции следующих этапов будут иметь префикс `/api/v1` и MVC Controllers.
Login/profile и JWT/JWKS пока не публикуются. Три auth операции этапа 2 — публичные.

| Метод | Маршрут | Ответ | Назначение |
| --- | --- | --- | --- |
| POST | `/api/v1/auth/register` | `202` generic | `{email,password,displayName?,clientId}` |
| POST | `/api/v1/auth/email/verification-request` | `202` generic | `{email}` |
| POST | `/api/v1/auth/email/verify` | `204` / `400 invalid_verification_token` | `{token}` |
| GET | `/health/live` | `200 application/json`, `{"status":"healthy"}` | Процесс принимает HTTP-запросы |
| GET | `/health/ready` | `200 {"status":"ready"}` / `503 {"status":"not_ready"}` | Подключение к PostgreSQL |
| GET | `/openapi/v1.json` | `200 application/json` в Development; `404` в Production | OpenAPI JSON |

```sh
curl --fail http://localhost:5080/health/live
curl --fail http://localhost:5080/health/ready
curl --fail http://localhost:5080/openapi/v1.json
```

Живость не проверяет PostgreSQL, SMTP или готовность аутентификации. Readiness
проверяет только подключение: наличие таблиц не заменяет отдельную deployment-команду
миграций. Readiness не выдаёт исключения, connection strings и детали БД.
`/health/ready` — internal endpoint: в production ограничьте его на proxy/firewall;
локальный Compose публикует весь API только на loopback.

JSON использует camelCase. Стандартные HTTP-ошибки обрабатываются через ProblemDetails
(`application/problem+json`), с `status`, `title`, `type`, `code` и `traceId`.
Коды: `not_found` (404), `method_not_allowed` (405), `validation_failed` (400),
доменный machine-readable код (400), `concurrency_conflict` (409), `request_failed`
(500 и остальные стандартные HTTP-ошибки). Для validation добавляется `errors`
с массивами сообщений по полям; этот контракт общий для MVC и CQRS validators.
Необработанные исключения дают generic 500 без stack trace и сообщения исключения.
Readiness использует отдельный краткий health response. В этапе 2 добавлены
`invalid_verification_token` (400), `registration_disabled` (503) и `rate_limited` (429).
Дубликат email при регистрации возвращает тот же 202, а не публичный 409.

## Регистрация, письмо и подтверждение

```sh
curl -i http://localhost:5080/api/v1/auth/register -H "Content-Type: application/json" --data '{"email":"user@example.com","password":"A long personal passphrase 42!","displayName":"Example","clientId":"native"}'
curl -i http://localhost:5080/api/v1/auth/email/verification-request -H "Content-Type: application/json" --data '{"email":"user@example.com"}'
```

Ответ в обоих случаях: `202 {"message":"If the request is eligible, a verification email will be sent."}`.
Регистрация создаёт PendingVerification, credential, token hash, encrypted outbox
и register audit event атомарно. Дубликат email не создаёт вторую запись или письмо.
Resend отправляет письмо только PendingVerification; unknown/Active/Disabled/Deactivated
дают тот же 202. На этапе 2 clientId проверяется по формату (1–100 символов),
селекция разрешённых клиентов/audience добавляется на этапе 3.

Откройте Mailpit `http://localhost:8025`, скопируйте токен из `#token=...` ссылки:

```sh
curl -i http://localhost:5080/api/v1/auth/email/verify -H "Content-Type: application/json" --data '{"token":"<token-from-Mailpit>"}'
```

Успех 204 переводит пользователя в Active и атомарно consumes token. Срок — 24 часа
от создания; boundary, malformed, unknown и уже использованный токен дают одинаковый
400. При resend старые токены сохраняются до истечения срока; первый успешный verify
активирует пользователя, после чего остальные уже не могут подтвердить его повторно.
Токен принимается только в JSON body. UI подтверждения не входит в этот backend.

Пароль: NFKC, 15–128 Unicode code points, до 1024 UTF-8 bytes до нормализации и
до 512 после неё; пробелы не обрезаются. Применяется локальный common-password denylist.
Лимиты по умолчанию: 30 auth requests/IP/60 секунд и 3 register/resend requests на
нормализованный email/60 секунд, общие для register/resend. Ответ 429 включает Retry-After.
Ограничение auth body в Kestrel — 16 KiB. CORS не включён.

Протокол Identity не является OAuth2/OIDC; OIDC discovery и OAuth endpoints не предоставляются.
