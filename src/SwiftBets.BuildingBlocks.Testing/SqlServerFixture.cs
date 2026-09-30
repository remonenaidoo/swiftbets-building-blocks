using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace SwiftBets.BuildingBlocks.Testing;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder(TestImages.SqlServer).Build();

    public string ServerConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    /// <summary>Creates an empty database and returns a connection string to it, so each test class gets its own.</summary>
    public async Task<string> CreateDatabaseAsync(string name)
    {
        await using (var connection = new SqlConnection(ServerConnectionString))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{name.Replace("]", "]]", StringComparison.Ordinal)}]";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        return new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = name }.ConnectionString;
    }
}
