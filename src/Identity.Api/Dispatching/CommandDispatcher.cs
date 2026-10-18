using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;

namespace Identity.Api.Dispatching;

public sealed class CommandDispatcher(IServiceProvider services) : ICommandDispatcher
{
    public async Task<TResult> Execute<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand<TResult>
    {
        ArgumentNullException.ThrowIfNull(command);
        ct.ThrowIfCancellationRequested();
        var errors = new List<FieldValidationError>();
        foreach (var validator in services.GetServices<ICommandValidator<TCommand>>())
        {
            errors.AddRange(await validator.Validate(command, ct));
        }
        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
        ct.ThrowIfCancellationRequested();
        return await services.GetRequiredService<ICommandHandler<TCommand, TResult>>().Handle(command, ct);
    }
}
