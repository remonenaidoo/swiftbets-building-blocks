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

        EnableReadCommittedSnapshot(connectionString);
        return Run(DeployChanges.To.SqlDatabase(connectionString).JournalToSqlTable("dbo", "SchemaVersions"), sources);
    }

    /// <summary>
    /// Readers see the last committed version instead of queueing behind writers (Azure SQL's default). Money paths do not
    /// rely on read blocking: they take explicit UPDLOCK/READPAST locks and unique indexes arbitrate duplicates.
    /// Runs outside a transaction, as ALTER DATABASE requires.
    /// </summary>
    private static void EnableReadCommittedSnapshot(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        if (connection.ExecuteScalar<bool>("SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()"))
        {
            return;
        }

        connection.Execute("ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE", commandTimeout: 120);
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
