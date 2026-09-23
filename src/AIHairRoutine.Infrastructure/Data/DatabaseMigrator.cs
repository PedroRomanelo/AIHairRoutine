using AIHairRoutine.Infrastructure.Config;
using DbUp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Infrastructure.Data;

/// <summary>Applies the embedded SQL scripts (DbUp) at startup. Idempotent and tracked in SchemaVersions.</summary>
public sealed class DatabaseMigrator(IOptions<DatabaseOptions> options, ILogger<DatabaseMigrator> logger)
{
    public void MigrateIfEnabled()
    {
        var opt = options.Value;
        if (!opt.Enabled || !opt.RunMigrationsOnStartup)
        {
            logger.LogInformation("Database migrations skipped (disabled or no connection string).");
            return;
        }

        var connectionString = opt.ConnectionString!;
        try
        {
            EnsureDatabase.For.SqlDatabase(connectionString);

            var upgrader = DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly)
                .WithTransactionPerScript()
                .LogToNowhere()
                .Build();

            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
                logger.LogError(result.Error, "Database migration failed.");
            else
                logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            // Don't crash the app if the DB is momentarily unavailable; the catalog degrades to empty.
            logger.LogError(ex, "Database migration threw; continuing without catalog.");
        }
    }
}
