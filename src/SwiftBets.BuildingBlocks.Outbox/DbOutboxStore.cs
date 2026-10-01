using System.Data.Common;
using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;

namespace SwiftBets.BuildingBlocks.Outbox;

/// <summary>Runs the outbox statements for one engine; Postgres statements live under <c>Sql/Postgres/</c>.</summary>
public sealed class DbOutboxStore : IOutboxStore
{
    private static readonly SqlResources Sql = SqlResources.For<DbOutboxStore>();
    private readonly Func<CancellationToken, Task<DbConnection>> _open;
    private readonly string _prefix;

    private DbOutboxStore(Func<CancellationToken, Task<DbConnection>> open, string prefix)
    {
        _open = open;
        _prefix = prefix;
    }

    public static DbOutboxStore SqlServer(ISqlConnectionFactory connections)
    {
        ArgumentNullException.ThrowIfNull(connections);
        return new(async ct => await connections.OpenAsync(ct).ConfigureAwait(false), string.Empty);
    }

    public static DbOutboxStore Postgres(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        return new(async ct => await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false), "Postgres.");
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(int batchSize, DateTimeOffset now, string owner, DateTimeOffset leaseUntil, CancellationToken cancellationToken)
    {
        await using var connection = await _open(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<OutboxMessage>(new CommandDefinition(
            Get("Outbox.ClaimBatch"),
            new { BatchSize = batchSize, Now = now, Owner = owner, LeaseUntil = leaseUntil },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return [.. rows.OrderBy(m => m.Sequence)];
    }

    public async Task MarkSentAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset now, string owner, CancellationToken cancellationToken)
    {
        await using var connection = await _open(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            Get("Outbox.MarkSent"),
            new { Ids = ids.ToArray(), Now = now, Owner = owner },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(Guid id, string owner, DateTimeOffset nextAttemptAt, string lastError, CancellationToken cancellationToken)
    {
        await using var connection = await _open(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            Get("Outbox.MarkFailed"),
            new { Id = id, Owner = owner, NextAttemptAt = nextAttemptAt, LastError = lastError },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<long> CountPendingAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _open(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(Get("Outbox.CountPending"), cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private string Get(string name) => Sql.Get(_prefix + name);
}
