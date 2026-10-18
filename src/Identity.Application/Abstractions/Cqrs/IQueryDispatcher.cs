namespace Identity.Application.Abstractions.Cqrs;

public interface IQueryDispatcher
{
    Task<TResult> Execute<TQuery, TResult>(TQuery query, CancellationToken ct = default)
        where TQuery : IQuery<TResult>;
}
