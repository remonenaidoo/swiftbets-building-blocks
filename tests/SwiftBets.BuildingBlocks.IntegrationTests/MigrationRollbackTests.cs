using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;

namespace SwiftBets.BuildingBlocks.IntegrationTests;

public sealed class MigrationRollbackTests(SqlServerFixture sqlServer, PostgresFixture postgres)
{
    private static readonly MigrationSource Source = new(typeof(MigrationRollbackTests).Assembly, 1);

    [Fact]
    public async Task Sql_server_rolls_back_newest_first_to_the_target_and_reapplies()
    {
        var database = await sqlServer.CreateDatabaseAsync("rb_" + Guid.NewGuid().ToString("N")[..8]);
        MigrationRunner.RunSqlServer(database, false, Source).Successful.ShouldBeTrue();

        var result = MigrationRollback.SqlServer(database, Source, targetVersion: 2);

        result.Successful.ShouldBeTrue(result.Error);
        result.RolledBack.Select(MigrationRollback.VersionOf).ShouldBe([4, 3]);
        await using var connection = new SqlConnection(database);
        (await TablesAsync(connection)).ShouldBe(["Widgets"]);
        MigrationRunner.RunSqlServer(database, false, Source).Successful.ShouldBeTrue();
        (await TablesAsync(connection)).ShouldBe(["Gadgets", "Gizmos", "Widgets"]);
    }

    [Fact]
    public async Task Postgres_rolls_back_to_the_target_and_reapplies()
    {
        var database = await CreatePostgresDatabaseAsync();
        MigrationRunner.RunPostgres(database, false, Source).Successful.ShouldBeTrue();

        var result = MigrationRollback.Postgres(database, Source, targetVersion: 3);

        result.Successful.ShouldBeTrue(result.Error);
        await using var connection = new NpgsqlConnection(database);
        (await TablesAsync(connection)).ShouldBe(["gadgets", "widgets"]);
        MigrationRunner.RunPostgres(database, false, Source).Successful.ShouldBeTrue();
        (await TablesAsync(connection)).ShouldBe(["gadgets", "gizmos", "widgets"]);
    }

    [Fact]
    public async Task Migration_without_a_rollback_script_stops_the_run_before_anything_changes()
    {
        var database = await sqlServer.CreateDatabaseAsync("rb_" + Guid.NewGuid().ToString("N")[..8]);
        MigrationRunner.RunSqlServer(database, false, Source).Successful.ShouldBeTrue();

        var result = MigrationRollback.SqlServer(database, Source, targetVersion: 0);

        result.Successful.ShouldBeFalse();
        result.Error!.ShouldContain("0002_widgets.sql");
        result.RolledBack.ShouldBeEmpty();
        await using var connection = new SqlConnection(database);
        (await TablesAsync(connection)).ShouldBe(["Gadgets", "Gizmos", "Widgets"]);
    }

    [Fact]
    public async Task Failing_rollback_keeps_that_migration_applied_and_stops()
    {
        var database = await sqlServer.CreateDatabaseAsync("rb_" + Guid.NewGuid().ToString("N")[..8]);
        MigrationRunner.RunSqlServer(database, false, Source).Successful.ShouldBeTrue();
        await using var connection = new SqlConnection(database);
        await connection.ExecuteAsync("DROP TABLE rb.Gizmos");

        var result = MigrationRollback.SqlServer(database, Source, targetVersion: 2);

        result.Successful.ShouldBeFalse();
        result.Error!.ShouldContain("0004_gizmos.sql");
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ScriptName LIKE '%0004_gizmos.sql'")).ShouldBe(1);
        (await TablesAsync(connection)).ShouldBe(["Gadgets", "Widgets"]);
    }

    [Theory]
    [InlineData("SwiftBets.Wallet.Migrator.Migrations.0004_reconciliation.sql", 4)]
    [InlineData("A.Migrations.Migrations.0120_x.sql", 120)]
    public void Version_is_the_leading_number_of_the_file_name(string resourceName, int version) =>
        MigrationRollback.VersionOf(resourceName).ShouldBe(version);

    [Fact]
    public void Script_without_a_leading_number_is_rejected() =>
        Should.Throw<FormatException>(() => MigrationRollback.VersionOf("A.Migrations.widgets.sql"));

    private static async Task<List<string>> TablesAsync(DbConnection connection) =>
        [.. (await connection.QueryAsync<string>(connection is SqlConnection
            ? "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('rb')"
            : "SELECT table_name FROM information_schema.tables WHERE table_schema = 'rb'")).Order(StringComparer.Ordinal)];

    private async Task<string> CreatePostgresDatabaseAsync()
    {
        var name = "rb_" + Guid.NewGuid().ToString("N")[..8];
        await using (var server = new NpgsqlConnection(postgres.ConnectionString))
        {
            await server.ExecuteAsync($"CREATE DATABASE {name}");
        }

        return new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = name }.ConnectionString;
    }
}
