namespace Identity.Infrastructure.Maintenance;

public sealed class SecurityMaintenanceOptions
{
    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 100;
    public int PollSeconds { get; set; } = 300;
    public int AuditRetentionDays { get; set; } = 90;
    public int TokenRetentionDays { get; set; } = 1;
    public int ProcessedOutboxRetentionDays { get; set; } = 7;
    public static bool IsValid(SecurityMaintenanceOptions options) => options.BatchSize is >= 1 and <= 1000
        && options.PollSeconds is >= 1 and <= 3600 && options.AuditRetentionDays is >= 1 and <= 3650
        && options.TokenRetentionDays is >= 1 and <= 30 && options.ProcessedOutboxRetentionDays is >= 1 and <= 365;
}
