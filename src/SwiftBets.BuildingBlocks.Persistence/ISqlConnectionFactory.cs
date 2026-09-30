using Microsoft.Data.SqlClient;

namespace SwiftBets.BuildingBlocks.Persistence;

public interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(CancellationToken cancellationToken);
}
