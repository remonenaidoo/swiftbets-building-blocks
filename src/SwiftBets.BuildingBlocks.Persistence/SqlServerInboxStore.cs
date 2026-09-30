using System.Data.Common;
using Dapper;

namespace SwiftBets.BuildingBlocks.Persistence;

public sealed class SqlServerInboxStore(TimeProvider time) : IInboxStore
{
    private static readonly string TryRecordSql = SqlResources.For<SqlServerInboxStore>().Get("Inbox.TryRecord");

    public async Task<bool> TryRecordAsync(DbTransaction transaction, string consumer, Guid eventId, CancellationToken cancellationToken)
    {
        var inserted = await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            TryRecordSql,
            new { Consumer = consumer, EventId = eventId, ProcessedAt = time.GetUtcNow() },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return inserted == 1;
    }
}
