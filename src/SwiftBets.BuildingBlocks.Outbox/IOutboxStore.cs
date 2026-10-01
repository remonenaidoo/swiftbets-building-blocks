namespace SwiftBets.BuildingBlocks.Outbox;

/// <summary>The relay's view of the outbox table; one implementation per database engine.</summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> ClaimAsync(int batchSize, DateTimeOffset now, string owner, DateTimeOffset leaseUntil, CancellationToken cancellationToken);

    Task MarkSentAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset now, string owner, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid id, string owner, DateTimeOffset nextAttemptAt, string lastError, CancellationToken cancellationToken);

    Task<long> CountPendingAsync(CancellationToken cancellationToken);
}

public sealed record OutboxMessage(long Sequence, Guid Id, string Topic, string MessageKey, byte[] Payload, string Headers, int AttemptCount);
