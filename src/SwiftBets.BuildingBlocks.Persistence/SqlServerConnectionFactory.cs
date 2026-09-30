using Microsoft.Data.SqlClient;

namespace SwiftBets.BuildingBlocks.Persistence;

public sealed class SqlServerConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
