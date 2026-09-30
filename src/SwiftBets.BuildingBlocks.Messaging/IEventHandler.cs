using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Messaging;

/// <summary>
/// Handles one consumed event. Throw <see cref="PoisonMessageException"/> for a message that can never succeed;
/// any other exception is treated as transient and the message is redelivered after a backoff.
/// </summary>
public interface IEventHandler<TPayload>
    where TPayload : IEventContract
{
    Task HandleAsync(ConsumedEvent<TPayload> message, CancellationToken cancellationToken);
}
