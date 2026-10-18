# HTTP API Identity — этапы 0–1

Бизнес-операции следующих этапов будут иметь префикс `/api/v1` и MVC Controllers.
Register/login/profile и JWT/JWKS пока не публикуются.

| Метод | Маршрут | Ответ | Назначение |
| --- | --- | --- | --- |
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
Readiness использует отдельный краткий health response. Коды auth, rate limits
и обработка публичных enumeration-sensitive конфликтов относятся к будущим этапам;
ошибки уникальности БД сейчас не преобразуются в публичный 409.

Протокол Identity не является OAuth2/OIDC; OIDC discovery и OAuth endpoints не предоставляются.
