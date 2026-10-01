using System.Data.Common;
using Dapper;

namespace SwiftBets.BuildingBlocks.Persistence;

public sealed class PostgresInboxStore(TimeProvider time) : IInboxStore
{
    private static readonly string TryRecordSql = SqlResources.For<PostgresInboxStore>().Get("Postgres.Inbox.TryRecord");

    public async Task<bool> TryRecordAsync(DbTransaction transaction, string consumer, Guid eventId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var inserted = await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            TryRecordSql,
            new { Consumer = consumer, EventId = eventId, ProcessedAt = time.GetUtcNow() },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return inserted == 1;
    }
}
