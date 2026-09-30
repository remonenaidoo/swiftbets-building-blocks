using Dapper;
using DbUp;
using Microsoft.Data.SqlClient;
using DbUp.Builder;
using DbUp.Engine;
using DbUp.Support;

namespace SwiftBets.BuildingBlocks.Persistence;

public static class MigrationRunner
{
    public static DatabaseUpgradeResult RunSqlServer(string connectionString, bool ensureDatabase, params MigrationSource[] sources)
    {
        if (ensureDatabase)
        {
            EnsureDatabase.For.SqlDatabase(connectionString);
        }

        return Run(DeployChanges.To.SqlDatabase(connectionString).JournalToSqlTable("dbo", "SchemaVersions"), sources);
    }

    public static DatabaseUpgradeResult RunPostgres(string connectionString, bool ensureDatabase, params MigrationSource[] sources)
    {
        if (ensureDatabase)
        {
            EnsureDatabase.For.PostgresqlDatabase(connectionString);
        }

        return Run(DeployChanges.To.PostgresqlDatabase(connectionString).JournalToPostgresqlTable("public", "schemaversions"), sources);
    }

    /// <summary>
    /// Maps a server login into the database as a member of <c>swiftbets_app</c>, the least-privilege role the
    /// service's own migrations grant schema rights to. The login itself is created by the platform, never here.
    /// </summary>
    public static async Task GrantSqlServerAppLoginAsync(string connectionString, string login, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            SqlResources.For<SqlServerInboxStore>().Get("Security.GrantAppLogin"),
            new { Login = login },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static DatabaseUpgradeResult Run(UpgradeEngineBuilder builder, MigrationSource[] sources)
    {
        foreach (var source in sources)
        {
            builder = builder.WithScriptsEmbeddedInAssembly(
                source.Assembly,
                source.Includes,
                new SqlScriptOptions { ScriptType = ScriptType.RunOnce, RunGroupOrder = source.Group });
        }

        return builder.WithTransactionPerScript().LogToConsole().Build().PerformUpgrade();
    }
}
