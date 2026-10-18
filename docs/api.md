# HTTP API Identity — этапы 0–3

Бизнес-операции используют префикс `/api/v1` и MVC Controllers. MVC по умолчанию
требует Bearer authentication; публичные actions явно помечены AllowAnonymous.
Refresh rotation/logout/profile/sessions API ещё не публикуются.

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
Начальный refresh token уже выдаётся, но endpoint его обмена появится на этапе 4.

Access JWT содержит только sub/iss/aud/jti/sid/client_id/iat/nbf/exp/token_use=access,
RS256 и kid. JWKS содержит только kty/use/alg/kid/n/e. Identity проверяет также
активность user/session по БД. Защищённые бизнес-операции появятся в последующих этапах;
сейчас этот путь покрыт test-only контроллером, отсутствующим в production assembly.

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
