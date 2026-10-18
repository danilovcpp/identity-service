using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Identity.Api.Configuration;

public sealed class AccountRateLimiter : IDisposable
{
    public PartitionedRateLimiter<string> Limiter { get; }

    public AccountRateLimiter(IOptions<AuthRateLimitOptions> options)
    {
        Limiter = PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.Value.AccountPermitLimit,
                Window = TimeSpan.FromSeconds(options.Value.WindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    public void Dispose() => Limiter.Dispose();
}
