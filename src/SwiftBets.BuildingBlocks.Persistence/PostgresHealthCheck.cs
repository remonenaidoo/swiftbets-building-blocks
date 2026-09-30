using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace SwiftBets.BuildingBlocks.Persistence;

public sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (NpgsqlException ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Postgres unreachable", ex);
        }
    }
}
