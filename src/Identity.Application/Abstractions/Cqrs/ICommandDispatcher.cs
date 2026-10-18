namespace Identity.Application.Abstractions.Cqrs;

public interface ICommandDispatcher
{
    Task<TResult> Execute<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand<TResult>;
}
