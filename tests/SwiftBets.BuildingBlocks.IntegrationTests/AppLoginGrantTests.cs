using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;

namespace SwiftBets.BuildingBlocks.IntegrationTests;

public sealed class AppLoginGrantTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Login_is_mapped_into_the_app_role_and_regranting_is_a_no_op()
    {
        var login = "svc_" + Guid.NewGuid().ToString("N")[..8];
        await using (var server = new SqlConnection(sql.ServerConnectionString))
        {
            await server.ExecuteAsync($"CREATE LOGIN [{login}] WITH PASSWORD = 'Str0ng!Passw0rd#1'");
        }

        var database = await sql.CreateDatabaseAsync("grant_" + Guid.NewGuid().ToString("N")[..8]);

        await MigrationRunner.GrantSqlServerAppLoginAsync(database, login, TestContext.Current.CancellationToken);
        await MigrationRunner.GrantSqlServerAppLoginAsync(database, login, TestContext.Current.CancellationToken);

        await using var connection = new SqlConnection(database);
        (await connection.ExecuteScalarAsync<int>("SELECT IS_ROLEMEMBER(N'swiftbets_app', @login)", new { login })).ShouldBe(1);
    }

    [Fact]
    public async Task Hostile_login_name_is_quoted_not_executed()
    {
        var database = await sql.CreateDatabaseAsync("grant_" + Guid.NewGuid().ToString("N")[..8]);

        await Should.ThrowAsync<SqlException>(() =>
            MigrationRunner.GrantSqlServerAppLoginAsync(database, "x]; DROP TABLE dbo.SchemaVersions; --", TestContext.Current.CancellationToken));
    }
}
