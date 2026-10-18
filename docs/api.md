# HTTP API Identity — этап 0

Бизнес-операции следующих этапов будут иметь префикс `/api/v1` и MVC Controllers.
Этап 0 не публикует register/login/profile, JWT/JWKS, readiness или демонстрационные business endpoints.

| Метод | Маршрут | Ответ | Назначение |
| --- | --- | --- | --- |
| GET | `/health/live` | `200 application/json`, `{"status":"healthy"}` | Процесс принимает HTTP-запросы |
| GET | `/openapi/v1.json` | `200 application/json` в Development; `404` в Production | OpenAPI JSON |

```sh
curl --fail http://localhost:5080/health/live
curl --fail http://localhost:5080/openapi/v1.json
```

Живость не проверяет PostgreSQL, SMTP или готовность аутентификации. Отдельный
`/health/ready` появится после подключения БД, с ограничением доступа на сетевом уровне.

JSON использует camelCase. Стандартные HTTP-ошибки обрабатываются через ProblemDetails
(`application/problem+json`), с `status`, `title`, `type`, `code` и `traceId`.
Сейчас коды: `not_found` для 404, `method_not_allowed` для 405, `request_failed` для остальных ошибок.
Необработанные исключения дают стандартный 500 без stack trace в ответе.
Полная таксономия прикладных ошибок и validation errors относится к следующим этапам.

Протокол Identity не является OAuth2/OIDC; OIDC discovery и OAuth endpoints не предоставляются.
