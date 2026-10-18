namespace Identity.Infrastructure.Maintenance;

public sealed class SessionCleanupOptions
{
    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 25;
    public int PollSeconds { get; set; } = 300;
    public int RetentionSeconds { get; set; } = 86400;
}
