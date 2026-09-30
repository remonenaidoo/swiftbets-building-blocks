using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<TPayload>(string topicBase, string key, EventEnvelope<TPayload> envelope, CancellationToken cancellationToken)
        where TPayload : IEventContract;

    Task PublishRawAsync(OutgoingMessage message, CancellationToken cancellationToken);
}
