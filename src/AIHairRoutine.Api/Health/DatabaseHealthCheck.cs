using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Data;
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AIHairRoutine.Api.Health;

/// <summary>Readiness check: verifies the database is reachable when it is configured.</summary>
public sealed class DatabaseHealthCheck(ISqlConnectionFactory factory, IOptions<DatabaseOptions> options)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return HealthCheckResult.Healthy("Database disabled; catalog degrades to empty.");

        try
        {
            await using var conn = factory.Create();
            await conn.ExecuteScalarAsync(new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));
            return HealthCheckResult.Healthy("Database reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unreachable.", ex);
        }
    }
}
