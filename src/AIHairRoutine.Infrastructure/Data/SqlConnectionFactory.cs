using AIHairRoutine.Infrastructure.Config;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Data;

public interface ISqlConnectionFactory
{
    SqlConnection Create();
}

/// <summary>Creates SQL Server connections from the configured connection string.</summary>
public sealed class SqlConnectionFactory(IOptions<DatabaseOptions> options) : ISqlConnectionFactory
{
    // Validated lazily so the DI graph still builds when the DB is disabled (catalog degrades to empty).
    public SqlConnection Create() => new(options.Value.ConnectionString
        ?? throw new InvalidOperationException("Database:ConnectionString is not configured."));
}
