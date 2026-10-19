# HTTP API Identity 1.0.0

Дополнение v1.1, этап 8: [административный auth API](admin-access.md#http--cookie--csrf-contract).
`GET /api/v1/admin/auth/csrf`, `POST /login`, `GET /me`, `POST /logout` (последние три
под тем же `/api/v1/admin/auth`). Отдельная Secure HttpOnly cookie-схема и CSRF на login/logout;
пользовательский Bearer не предоставляет admin-доступ. Этап9 добавляет пять
[read-only admin GET endpoints](admin-read-api.md) для dashboard, users/details/sessions и audit.
Изменяющие административные операции и frontend пока не реализованы.

## Первый end-to-end сценарий

После [локального запуска](../README.md#быстрый-локальный-запуск), с отдельным dev
email/паролем. Все значения tokens ниже — placeholders; не сохраняйте реальные
секреты в shell history/CI logs. Для защищённого интерактивного клиента используйте
память/защищённое хранилище, не копируйте реальные значения в общие скрипты.

```sh
curl -i http://localhost:5080/api/v1/auth/register -H "Content-Type: application/json" --data '{"email":"reader@identity.example","password":"<strong-password-15-plus-codepoints>","displayName":"Reader","clientId":"native"}'
```

Ожидается 202. Откройте Mailpit (`http://localhost:8025`), найдите письмо Identity,
скопируйте token из fragment `#token=...`. Переход на localhost:3000 не нужен: эта
страница не поставляется. Отправьте token в JSON, затем войдите:

```sh
curl -i http://localhost:5080/api/v1/auth/email/verify -H "Content-Type: application/json" --data '{"token":"<verification-token-from-Mailpit>"}'
curl -i http://localhost:5080/api/v1/auth/login -H "Content-Type: application/json" --data '{"email":"reader@identity.example","password":"<same-strong-password>","clientId":"native"}'
```

Verify даёт 204, login — 200 c accessToken/refreshToken. Обменяйте login refresh
один раз; сохраните **оба новых** значения из ответа, старый refresh больше не используйте:

```sh
curl -i http://localhost:5080/api/v1/auth/refresh -H "Content-Type: application/json" --data '{"refreshToken":"<login-refresh>"}'
curl -i http://localhost:5080/api/v1/users/me -H "Authorization: Bearer <new-access>"
curl -i http://localhost:5080/api/v1/auth/logout -H "Content-Type: application/json" --data '{"refreshToken":"<new-refresh>"}'
curl -i http://localhost:5080/api/v1/auth/refresh -H "Content-Type: application/json" --data '{"refreshToken":"<new-refresh>"}'
```

Результаты: 200 token pair → 200 собственный профиль → 204 → 401 invalid_refresh_token.
Повтор старого consumed refresh до logout считается replay и отзывает всё семейство.
Автоматический эквивалент, включая Mailpit и cleanup: `scripts/Test-Stage7Smoke.ps1`.

## Транспорт и ошибки

Production business API требует HTTPS: HTTP даёт 400 `https_required` без redirect.
HTTP `/health/live` и `/health/ready` разрешён для внутренних probes; readiness должен
быть закрыт на proxy/firewall. За TLS terminator используйте только явные trusted
proxy IP и оба X-Forwarded-For/X-Forwarded-Proto (см. deployment). CORS выключен.

Ответы содержат `X-Correlation-ID`, совпадающий с ProblemDetails.traceId и audit
CorrelationId операции. Входной X-Correlation-ID игнорируется; используется W3C trace
или созданный сервером ID. Корреляция не является средством аутентификации.
`X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, no-store для business
API; production HTTPS также возвращает HSTS max-age=31536000 без preload/includeSubDomains.

JSON errors сохраняют `application/problem+json` также при Accept text/html/XML.
400: validation_failed / invalid_request / invalid_*_token / https_required;
401: unauthorized / invalid_credentials / invalid_refresh_token; 403: forbidden;
404: not_found / session_not_found; 405: method_not_allowed; 413: request_too_large;
429: rate_limited с Retry-After; 500: request_failed. Existing 409 concurrency_conflict
и 503 feature-disabled/readiness сохраняются. У malformed JSON/model binding
`errors.request` содержит постоянное безопасное сообщение без отражения входа;
CQRS validation возвращает постоянные сообщения по известным полям.
Auth/profile body ограничен 16 KiB, включая chunked запросы без Content-Length;
oversized input отклоняется до model binding. Поля дополнительно ограничены validators.

Бизнес-операции используют префикс `/api/v1` и MVC Controllers. MVC по умолчанию
требует Bearer authentication; публичные actions явно помечены AllowAnonymous.

## Профиль, пароль и деактивация

GET/PATCH `/api/v1/users/me`, POST `/api/v1/auth/password/change` и
POST `/api/v1/users/me/deactivate` требуют действующего Bearer. Владелец и текущая
сессия берутся только из проверенных sub/sid; endpoints для чужого userId отсутствуют.

```sh
curl -i http://localhost:5080/api/v1/users/me -H "Authorization: Bearer <access-token>"
curl -i http://localhost:5080/api/v1/users/me -X PATCH -H "Authorization: Bearer <access-token>" -H "Content-Type: application/json" --data '{"displayName":"Example"}'
curl -i http://localhost:5080/api/v1/auth/password/change -H "Authorization: Bearer <access-token>" -H "Content-Type: application/json" --data '{"currentPassword":"<current-password>","newPassword":"<new-password>"}'
curl -i http://localhost:5080/api/v1/auth/password/forgot -H "Content-Type: application/json" --data '{"email":"user@example.com"}'
curl -i http://localhost:5080/api/v1/auth/password/reset -H "Content-Type: application/json" --data '{"token":"<reset-token-from-email>","newPassword":"<new-password>"}'
curl -i http://localhost:5080/api/v1/users/me/deactivate -H "Authorization: Bearer <access-token>" -H "Content-Type: application/json" --data '{"password":"<current-password>"}'
```

GET/PATCH возвращают 200 и только `id`, `email`, `displayName`, `emailVerified`,
`status` (`"active"`), `createdAt`, с no-store. PATCH принимает исключительно
`displayName`: строку до 120 символов после Trim без control characters либо null
для очистки. Пустая/whitespace строка, отсутствующее поле и неизвестные поля
(включая email/userId) дают 400. Email неизменяемый.

Change требует текущий пароль; неверный — generic 401 invalid_credentials.
Новый пароль проходит ту же политику, что регистрация; NFKC и сохранение пробелов
одинаковы при hash/verify. Неверный новый пароль даёт 400 validation_failed.

Forgot — публичный запрос с одинаковым 202 и сообщением
`If the request is eligible, a password reset email will be sent.` для неизвестного,
pending/disabled/deactivated/active пользователя, а также при выключенном PasswordReset.
Неверная форма email — 400; IP/account limit — 429. Письмо создаётся только для Active,
включая временно заблокированного пользователя, на сохранённый адрес аккаунта.
Новый запрос аннулирует предыдущие неистёкшие reset links. Recipient и URL не выбираются
клиентом: template задаёт оператор, Identity вставляет только token.

Reset link действует 30 минут и используется один раз. Token передаётся только в body;
неверный, истёкший, consumed, token другой цели, token неактивного пользователя и
выключенный reset дают одинаковый 400 invalid_password_reset_token. Нарушение политики
нового пароля даёт 400 validation_failed без потребления пригодного token.
Reset снимает временную login-блокировку; disabled/deactivated аккаунт не активируется.

Успешные change/reset/deactivate дают пустой 204. Каждая операция атомарно отзывает
**все** сессии и refresh tokens пользователя, включая текущую, и аннулирует остальные
reset links. Автоматического login или новой пары токенов нет. Потребуется новый login;
на Identity прежний JWT сразу отклоняется, в сторонних API действует до exp (+skew).
Change/deactivate дополнительно ограничены локальным account limit по проверенному sub.
Деактивация требует текущий пароль и является терминальным состоянием: login даёт
generic 401, forgot — generic 202 без письма. Это не удаление PII: email, credential
и audit сохраняются; endpoint восстановления/удаления данных не добавлен.

## Refresh, logout и собственные сессии

Все токены в примерах — placeholders. Не сохраняйте реальные значения в shell history
и публичных логах; используйте защищённое хранилище/ввод вашего клиента.

```sh
curl -i http://localhost:5080/api/v1/auth/refresh -H "Content-Type: application/json" --data '{"refreshToken":"<current-refresh-token>"}'
curl -i http://localhost:5080/api/v1/auth/logout -H "Content-Type: application/json" --data '{"refreshToken":"<current-refresh-token>"}'
curl -i http://localhost:5080/api/v1/auth/logout-all -X POST -H "Authorization: Bearer <access-token>"
curl -i http://localhost:5080/api/v1/sessions -H "Authorization: Bearer <access-token>"
curl -i http://localhost:5080/api/v1/sessions/<own-session-id> -X DELETE -H "Authorization: Bearer <access-token>"
```

Refresh возвращает тот же token response, что login, с новой парой и no-store.
Он сохраняет sid/client_id/audience, обновляет jti и lastUsedAt. ClientId/audience
из дополнительных JSON-полей не используются: источник — сохранённая сессия.
Новый refresh действует не дольше absoluteExpiresAt, даже если осталось меньше 7 дней.
Unknown/expired/revoked token, expired/revoked session, неактивный/temporarily locked
user, выключенный клиент или изменившаяся audience дают одинаковый 401 invalid_refresh_token.
Некорректное значение refreshToken также даёт эту 401; malformed JSON — стандартный 400.
При выключенном Jwt:Enabled refresh также возвращает generic 401.

Повторный обмен consumed token — replay: отзывает **всё семейство**, включая уже
выданную замену, и записывает audit. Даже истёкший consumed token остаётся evidence
до cleanup. Из двух параллельных refresh один может получить 200, второй получит
401 и отзовёт также токен победителя. Клиент обязан сериализовать refresh одной сессии
и атомарно заменить обе сохранённые строки. Grace window/retry старого токена нет.
Если после commit потерян ответ, автоматический повтор старого refresh приведёт
к отзыву семейства: выполните новый login. Сбой до commit откатывает изменения.

Logout принимает также consumed/expired token известного семейства и отзывает его
без выдачи новой пары. Для unknown/malformed/already revoked значения — тот же пустой
204. Logout-all и DELETE требуют действующего Bearer. После отзыва текущей сессии
её JWT больше не подходит для Identity, поэтому повторный protected запрос даст 401.
DELETE уже отозванной своей сессии с другим действующим собственным JWT даёт 204.
Чужая/отсутствующая сессия — одинаковый 404 session_not_found.

GET sessions возвращает массив объектов с `id`, `clientId`, `createdAt`, `lastUsedAt`,
`absoluteExpiresAt`, `userAgent`, `isCurrent` (по sid). Сортировка lastUsedAt descending,
потом id. Токены/хеши/профили чужих пользователей не возвращаются. Query-параметр userId
не выбирает владельца: он всегда берётся из проверенного sub. Revoked/expired и legacy
sessions без audience binding в массив не входят. Ответ no-store.

Отзыв немедленно проверяется Identity через БД. Внешний API, проверяющий подпись
локально, продолжает принимать старый access JWT до exp (+skew). Это не OAuth/OIDC.

## Login и JWT/JWKS

После подтверждения email:

```sh
curl -i http://localhost:5080/api/v1/auth/login -H "Content-Type: application/json" --data '{"email":"user@example.com","password":"A long personal passphrase 42!","clientId":"native"}'
curl --fail http://localhost:5080/.well-known/jwks.json
```

Login возвращает `200 {"accessToken":"...","refreshToken":"...","tokenType":"Bearer","expiresIn":900}`,
с `Cache-Control: no-store`. Неизвестный email, неверный пароль, неподтверждённый/
disabled/deactivated/временно заблокированный пользователь и неизвестный/выключенный
client дают одинаковый `401 invalid_credentials`. Некорректная форма — 400, rate limit —
429 с Retry-After, выключенный Jwt:Enabled — 503 login_disabled. Параметры блокировки
описаны в deployment docs. Пароль не обрезается; новые требования password policy
не применяются к уже существующему паролю при login.

Dev Compose задаёт issuer `https://identity.localhost`, clients `native` → `identity-native`
и `backend` → `identity-backend`. HTTPS issuer — идентификатор токена; сам Compose
доступен по локальному HTTP. Production transport должен быть HTTPS.
`clientId` публичный; он выбирает audience и не аутентифицирует приложение.
Refresh token — 32 random bytes в base64url; сохраняйте в secure OS/server storage.
Refresh token обменивается один раз; клиент обязан сериализовать refresh одной сессии.

Access JWT содержит только sub/iss/aud/jti/sid/client_id/iat/nbf/exp/token_use=access,
RS256 и kid. JWKS содержит только kty/use/alg/kid/n/e. Identity проверяет также
активность user/session и сохранённую audience по БД. Bearer-защищены sessions API,
logout-all, профиль, change и deactivate. Refresh/logout передают непрозрачный токен только в JSON body.

Пример для отдельного .NET API: загрузите JWKS из доверенного фиксированного HTTPS URL
при старте и настройте стандартный JwtBearer. Пример использует только public keys;
issuer/audience задаёт сам потребитель. Не указывайте Authority: Identity не публикует
OIDC discovery. Кэшируйте/обновляйте JWKS с ограниченным timeout; перед ротацией
предварительно обновите ключи потребителей, либо реализуйте bounded refresh по неизвестному kid.

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
var jwks = new JsonWebKeySet(await http.GetStringAsync("https://identity.example/.well-known/jwks.json"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.IncludeErrorDetails = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = "https://identity.example",
        ValidAudience = "identity-native", // только собственная audience этого API
        ValidateIssuer = true, ValidateAudience = true,
        IgnoreTrailingSlashWhenValidatingAudience = false,
        IssuerSigningKeys = jwks.GetSigningKeys(),
        ValidateIssuerSigningKey = true, RequireSignedTokens = true,
        TryAllIssuerSigningKeys = false, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        RequireExpirationTime = true, ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            if (context.Principal?.FindFirst("token_use")?.Value != "access")
                context.Fail("Invalid access token.");
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
// app.UseAuthentication(); app.UseAuthorization(); защищённые Controllers: [Authorize]
```

Настройка JwtBearer описана в [Microsoft documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).
Сервис использует собственный REST token protocol, без OAuth2/OIDC/SSO.

| Метод | Маршрут | Ответ | Назначение |
| --- | --- | --- | --- |
| POST | `/api/v1/auth/register` | `202` generic | `{email,password,displayName?,clientId}` |
| POST | `/api/v1/auth/email/verification-request` | `202` generic | `{email}` |
| POST | `/api/v1/auth/email/verify` | `204` / `400 invalid_verification_token` | `{token}` |
| POST | `/api/v1/auth/login` | `200` / generic `401 invalid_credentials` | `{email,password,clientId}` |
| POST | `/api/v1/auth/refresh` | `200` / generic `401 invalid_refresh_token` | `{refreshToken}`; rotation, replay отзывает семейство |
| POST | `/api/v1/auth/logout` | `204` | `{refreshToken}`; public, идемпотентно |
| POST | `/api/v1/auth/logout-all` | `204` | Bearer; все собственные сессии, включая текущую |
| GET | `/api/v1/users/me` | `200` | Bearer; собственный профиль |
| PATCH | `/api/v1/users/me` | `200` | Bearer; только `{displayName}` (null очищает) |
| POST | `/api/v1/users/me/deactivate` | `204` / generic `401` | Bearer; `{password}`, терминальная деактивация |
| POST | `/api/v1/auth/password/change` | `204` / generic `401` | Bearer; `{currentPassword,newPassword}` |
| POST | `/api/v1/auth/password/forgot` | `202` generic | Public; `{email}` |
| POST | `/api/v1/auth/password/reset` | `204` / `400 invalid_password_reset_token` | Public; `{token,newPassword}` |
| GET | `/api/v1/sessions` | `200` | Bearer; массив собственных активных сессий |
| DELETE | `/api/v1/sessions/{id}` | `204` / `404 session_not_found` | Bearer; своя сессия, чужая/отсутствующая неразличимы |
| GET | `/.well-known/jwks.json` | `200` | Публичный JWK Set; cache max-age=300 |
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
дают тот же 202. На регистрации clientId проверяется по формату (1–100 символов);
при login принимаются только включённые клиенты из Identity:Clients.

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
