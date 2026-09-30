using System.Text;
using Confluent.Kafka;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.IntegrationTests;

/// <summary>Proves the pinned Confluent.Kafka client and Redpanda version negotiate produce, consume and commit cleanly.</summary>
public sealed class BrokerCompatibilityTests(RedpandaFixture redpanda)
{
    [Fact]
    public async Task Idempotent_producer_consumer_and_manual_commit_round_trip()
    {
        var environment = Kafka.UniqueEnvironment();
        var topic = TopicName.For("test.thing-happened.v1", environment).Value;
        await redpanda.CreateTopicsAsync(3, topic);
        var options = Kafka.Options(redpanda.BootstrapServers, environment);
        using var publisher = Kafka.Publisher(options);
        var envelope = EventEnvelope<TestEvent>.Create(new TestEvent("x", 1), DateTimeOffset.UtcNow, "corr-broker");

        await publisher.PublishAsync("test.thing-happened.v1", "key-1", envelope, TestContext.Current.CancellationToken);

        using var consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = redpanda.BootstrapServers,
            GroupId = "compat-" + environment,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        consumer.Subscribe(topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        result.ShouldNotBeNull();
        consumer.Commit(result);

        EnvelopeSerializer.Deserialize<TestEvent>(result.Message.Value)!.Id.ShouldBe(envelope.Id);
        Encoding.UTF8.GetString(result.Message.Headers.GetLastBytes(MessageHeaders.CorrelationId)).ShouldBe("corr-broker");
        consumer.Committed([result.TopicPartition], TimeSpan.FromSeconds(10)).Single().Offset.Value.ShouldBe(result.Offset.Value + 1);
    }
}
