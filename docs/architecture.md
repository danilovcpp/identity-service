# Архитектура Identity — этап 0

Граф project references соответствует разделу 3 спецификации:

```text
Domain          -> нет зависимостей
Application     -> Domain
Infrastructure  -> Application, Domain
Api             -> Application, Infrastructure
```

Domain пока не содержит сущностей, пакетов или зависимостей от framework.
Application и Infrastructure предоставляют точки регистрации `AddApplication()`
и `AddInfrastructure()`. В этапе 0 регистрировать use cases и адаптеры ещё нечего;
методы не подменяют будущие сервисы фиктивными реализациями.
Application использует только DI abstractions, без ASP.NET, EF, IConfiguration или HttpContext.

API — composition root. DI проверяет lifetime scopes и граф зарегистрированных
сервисов при сборке host во всех средах. Контроллер живости использует только HTTP DTO.
Абстрактный `ApiController` задаёт `[ApiController]`, JSON и route `api/v1/[controller]`
для бизнес-контроллеров следующих этапов. Сам по себе он endpoints не создаёт.
HealthController использует отдельный операционный маршрут `/health/live`.
Minimal API бизнес-endpoints отсутствуют; `MapOpenApi()` обслуживает только документацию.

По разделу 10 собственные типизированные CQRS-контракты, диспетчеры в API,
валидаторы, TimeProvider, доменная модель, EF Core и миграции относятся к этапу 1.
MediatR, generic repository, ASP.NET Core Identity user management не подключены.

Тесты этапа 0 проверяют ссылки проектов, границу Domain/Application, реальную
композицию MVC host, dev/prod-доступность OpenAPI и HTTP-ответы через WebApplicationFactory.
Они не являются свидетельством корректности работы с БД: такой код ещё отсутствует.
