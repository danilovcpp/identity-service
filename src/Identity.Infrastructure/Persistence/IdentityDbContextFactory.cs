using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("Database__ConnectionString");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("Set Database__ConnectionString for EF tooling. No connection string is stored in source control.");
        }
        var options = new DatabaseOptions { ConnectionString = connection };
        if (!DatabaseOptions.IsValid(options))
        {
            throw new InvalidOperationException("Database configuration is invalid; error detail and parameter logging must be disabled.");
        }
        return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connection).Options);
    }
}
