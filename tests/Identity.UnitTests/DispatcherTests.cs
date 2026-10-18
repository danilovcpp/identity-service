using Identity.Api.Dispatching;
using Identity.Application.Abstractions.Cqrs;
using Identity.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.UnitTests;

public sealed class DispatcherTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DispatchInvokesHandlerExactlyOnceAfterValidatorsAndPreservesCancellation(bool command)
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new RecordingHandler();
        var validator = new RecordingValidator([]);
        await using var provider = BuildProvider(handler, validator);
        using var scope = provider.CreateScope();
        var result = await Execute(scope.ServiceProvider, command, cancellation.Token);
        Assert.Equal(42, result);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, validator.Calls);
        Assert.Equal(cancellation.Token, handler.Token);
        Assert.Equal(cancellation.Token, validator.Token);
        Assert.True(handler.ValidatorFinished);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidRequestDoesNotResolveOrExecuteHandler(bool command)
    {
        var handler = new RecordingHandler();
        var errors = new[] { new FieldValidationError("value", "invalid_value", "The value is invalid.") };
        await using var provider = BuildProvider(handler, new RecordingValidator(errors));
        using var scope = provider.CreateScope();
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => Execute(scope.ServiceProvider, command, CancellationToken.None));
        Assert.Equal(errors, exception.Errors);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationPreventsExecution(bool command)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new RecordingHandler();
        var validator = new RecordingValidator([]);
        await using var provider = BuildProvider(handler, validator);
        using var scope = provider.CreateScope();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Execute(scope.ServiceProvider, command, cancellation.Token));
        Assert.Equal(0, validator.Calls);
        Assert.Equal(0, handler.Calls);
    }

    private static Task<int> Execute(IServiceProvider services, bool command, CancellationToken cancellationToken) => command
        ? services.GetRequiredService<ICommandDispatcher>().Execute<Request, int>(new Request(), cancellationToken)
        : services.GetRequiredService<IQueryDispatcher>().Execute<Request, int>(new Request(), cancellationToken);

    private static ServiceProvider BuildProvider(RecordingHandler handler, RecordingValidator validator)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<ICommandHandler<Request, int>>(_ =>
        {
            handler.ValidatorFinished = validator.Calls == 1;
            return handler;
        });
        services.AddScoped<IQueryHandler<Request, int>>(_ =>
        {
            handler.ValidatorFinished = validator.Calls == 1;
            return handler;
        });
        services.AddScoped<ICommandValidator<Request>>(_ => validator);
        services.AddScoped<IQueryValidator<Request>>(_ => validator);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private sealed record Request : ICommand<int>, IQuery<int>;

    private sealed class RecordingHandler : ICommandHandler<Request, int>, IQueryHandler<Request, int>
    {
        public int Calls { get; private set; }
        public CancellationToken Token { get; private set; }
        public bool ValidatorFinished { get; set; }
        public Task<int> Handle(Request request, CancellationToken cancellationToken)
        {
            Calls++;
            Token = cancellationToken;
            return Task.FromResult(42);
        }
    }

    private sealed class RecordingValidator(IReadOnlyList<FieldValidationError> errors) : ICommandValidator<Request>, IQueryValidator<Request>
    {
        public int Calls { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<IReadOnlyList<FieldValidationError>> Validate(Request request, CancellationToken cancellationToken)
        {
            Calls++;
            Token = cancellationToken;
            return Task.FromResult(errors);
        }
    }
}
