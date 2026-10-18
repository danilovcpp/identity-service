namespace Identity.Api.Configuration;

public sealed class AuthRateLimitOptions
{
    public int IpPermitLimit { get; set; } = 30;
    public int AccountPermitLimit { get; set; } = 3;
    public int WindowSeconds { get; set; } = 60;
}
