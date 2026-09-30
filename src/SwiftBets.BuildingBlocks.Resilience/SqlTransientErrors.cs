using Microsoft.Data.SqlClient;

namespace SwiftBets.BuildingBlocks.Resilience;

public static class SqlTransientErrors
{
    private static readonly HashSet<int> TransientNumbers =
    [
        -2, 53, 233, 1205, 1222, 4221, 10053, 10054, 10060,
        40197, 40501, 40613, 49918, 49919, 49920,
    ];

    public static bool IsTransient(SqlException exception) =>
        exception.Errors.Cast<SqlError>().Any(e => TransientNumbers.Contains(e.Number));

    public static bool IsTransient(int errorNumber) => TransientNumbers.Contains(errorNumber);
}
