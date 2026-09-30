using System.Data.Common;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Outbox;

public interface IOutbox
{
    /// <summary>Stores the event in the caller's transaction; it is published only if that transaction commits.</summary>
    Task EnqueueAsync<TPayload>(DbTransaction transaction, string topicBase, string key, EventEnvelope<TPayload> envelope, CancellationToken cancellationToken)
        where TPayload : IEventContract;
}
