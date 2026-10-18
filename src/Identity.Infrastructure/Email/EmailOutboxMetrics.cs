using System.Diagnostics.Metrics;

namespace Identity.Infrastructure.Email;

public sealed class EmailOutboxMetrics : IDisposable
{
    private readonly Meter _meter = new("Identity.Email");
    private long _pending;
    private long _failed;
    private readonly Counter<long> _delivered;
    private readonly Counter<long> _failures;

    public EmailOutboxMetrics()
    {
        _meter.CreateObservableGauge("identity.email.outbox.pending", () => Interlocked.Read(ref _pending));
        _meter.CreateObservableGauge("identity.email.outbox.failed", () => Interlocked.Read(ref _failed));
        _delivered = _meter.CreateCounter<long>("identity.email.delivered");
        _failures = _meter.CreateCounter<long>("identity.email.delivery_failures");
    }

    public void Delivered() => _delivered.Add(1);
    public void Failed() => _failures.Add(1);
    public void Backlog(long pending, long failed) { Interlocked.Exchange(ref _pending, pending); Interlocked.Exchange(ref _failed, failed); }
    public void Dispose() => _meter.Dispose();
}
