namespace Identity.Infrastructure.Security;

public sealed class PasswordHashOptions
{
    public int IterationCount { get; set; } = 600_000;
}
