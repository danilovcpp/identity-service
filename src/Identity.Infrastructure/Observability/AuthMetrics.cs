using System.Diagnostics.Metrics;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Observability;

public sealed class AuthMetrics : IDisposable
{
    private readonly Meter _meter = new("Identity.Auth");
    private readonly Counter<long> _events;
    private long _activeSessions;
    private long _sampledAt;
    private readonly ILogger<AuthMetrics> _logger;
    public AuthMetrics(ILogger<AuthMetrics> logger)
    {
        _logger = logger;
        _events = _meter.CreateCounter<long>("identity.auth.events");
        _meter.CreateObservableGauge("identity.auth.sessions.active", () => Interlocked.Read(ref _activeSessions));
        _meter.CreateObservableGauge("identity.auth.sessions.sampled_at", () => Interlocked.Read(ref _sampledAt), "s");
    }
    public void Committed(IEnumerable<SecurityAuditEvent> events)
    {
        foreach (var entry in events)
        {
            try
            {
                // EventType is a Domain whitelist. ClientId/user/correlation IDs never become metric labels.
                _events.Add(1, new KeyValuePair<string, object?>("event", entry.EventType));
                _logger.LogInformation("Security event {EventType} committed; trace {TraceId}", entry.EventType, entry.CorrelationId);
            }
            // Telemetry is best-effort; a listener/logger must never turn a committed operation into a failure.
            catch (Exception) { }
        }
    }
    public void Sessions(long count, DateTimeOffset now)
    {
        Interlocked.Exchange(ref _activeSessions, count);
        Interlocked.Exchange(ref _sampledAt, now.ToUnixTimeSeconds());
    }
    public void Dispose() => _meter.Dispose();
}
