using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;

namespace Identity.Api.Dispatching;

public sealed class QueryDispatcher(IServiceProvider services) : IQueryDispatcher
{
    public async Task<TResult> Execute<TQuery, TResult>(TQuery query, CancellationToken ct = default)
        where TQuery : IQuery<TResult>
    {
        ArgumentNullException.ThrowIfNull(query);
        ct.ThrowIfCancellationRequested();
        var errors = new List<FieldValidationError>();
        foreach (var validator in services.GetServices<IQueryValidator<TQuery>>())
        {
            errors.AddRange(await validator.Validate(query, ct));
        }
        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
        ct.ThrowIfCancellationRequested();
        return await services.GetRequiredService<IQueryHandler<TQuery, TResult>>().Handle(query, ct);
    }
}
