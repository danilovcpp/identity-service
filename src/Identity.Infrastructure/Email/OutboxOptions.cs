namespace Identity.Infrastructure.Email;

public sealed class OutboxOptions
{
    public bool Enabled { get; set; }
    public int BatchSize { get; set; } = 10;
    public double PollSeconds { get; set; } = 5;
    public int MaxAttempts { get; set; } = 8;
    public double BaseRetrySeconds { get; set; } = 10;
}
