using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Messaging;

/// <summary>The latest value per key of a compacted topic, held in memory and kept current by a background reader.</summary>
public interface ICompactedState<TPayload>
    where TPayload : IEventContract
{
    /// <summary>True once every partition has been read to the end it had at start-up.</summary>
    bool IsReady { get; }

    Task WaitUntilReadyAsync(CancellationToken cancellationToken);

    bool TryGet(string key, out TPayload value);

    IReadOnlyDictionary<string, TPayload> Snapshot();
}
