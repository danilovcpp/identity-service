using Npgsql;

namespace Identity.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; set; } = string.Empty;

    public static bool IsValid(DatabaseOptions options)
    {
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(options.ConnectionString);
            return !string.IsNullOrWhiteSpace(connection.Host) && !string.IsNullOrWhiteSpace(connection.Database)
                && !string.IsNullOrWhiteSpace(connection.Username) && !connection.IncludeErrorDetail && !connection.LogParameters;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
