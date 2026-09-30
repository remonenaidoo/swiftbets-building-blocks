using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Messaging;

public sealed record ConsumedEvent<TPayload>(
    EventEnvelope<TPayload> Envelope,
    string Topic,
    int Partition,
    long Offset,
    IReadOnlyDictionary<string, string> Headers)
    where TPayload : IEventContract;
