using DbUp;
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
