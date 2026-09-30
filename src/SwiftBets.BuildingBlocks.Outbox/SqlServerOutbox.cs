using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Outbox;

public sealed class SqlServerOutbox(IOptions<KafkaOptions> kafka, TimeProvider time) : IOutbox
{
    private static readonly string EnqueueSql = SqlResources.For<SqlServerOutbox>().Get("Outbox.Enqueue");

    public Task EnqueueAsync<TPayload>(DbTransaction transaction, string topicBase, string key, EventEnvelope<TPayload> envelope, CancellationToken cancellationToken)
        where TPayload : IEventContract
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction.Connection!.ExecuteAsync(new CommandDefinition(
            EnqueueSql,
            new
            {
                envelope.Id,
                Topic = TopicName.For(topicBase, kafka.Value.Environment).Value,
                MessageKey = key,
                EventType = envelope.Type,
                Payload = EnvelopeSerializer.Serialize(envelope),
                Headers = JsonSerializer.Serialize(EnvelopeSerializer.Headers(envelope)),
                CreatedAt = time.GetUtcNow(),
            },
            transaction,
            cancellationToken: cancellationToken));
    }
}
