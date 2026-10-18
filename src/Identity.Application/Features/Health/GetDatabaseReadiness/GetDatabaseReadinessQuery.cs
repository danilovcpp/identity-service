using Identity.Application.Abstractions.Cqrs;

namespace Identity.Application.Features.Health.GetDatabaseReadiness;

public sealed record GetDatabaseReadinessQuery : IQuery<bool>;
