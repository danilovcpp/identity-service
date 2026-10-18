using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Identity.IntegrationTests;

internal sealed class SecretLogCapture : ILoggerProvider
{
    public ConcurrentBag<string> Lines { get; } = [];
    public ILogger CreateLogger(string categoryName) => new Capture(Lines);
    public void Dispose() { }
    private sealed class Capture(ConcurrentBag<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }
}
