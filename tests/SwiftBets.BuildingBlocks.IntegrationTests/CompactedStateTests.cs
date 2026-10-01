using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.IntegrationTests;

public sealed class CompactedStateTests(RedpandaFixture redpanda)
{
    private const string TopicBase = "test.thing-state.v1";

    [Fact]
    public async Task Reads_existing_records_before_ready_then_follows_updates_and_tombstones()
    {
        var kafka = Kafka.Options(redpanda.BootstrapServers, Kafka.UniqueEnvironment());
        var topic = TopicName.For(TopicBase, kafka.Value.Environment).Value;
        await redpanda.CreateTopicsAsync(3, topic);
        using var publisher = Kafka.Publisher(kafka);
        await publisher.PublishAsync(TopicBase, "a", Envelope("a", 1), CancellationToken.None);
        await publisher.PublishAsync(TopicBase, "a", Envelope("a", 2), CancellationToken.None);
        await publisher.PublishAsync(TopicBase, "b", Envelope("b", 1), CancellationToken.None);
        await publisher.PublishRawAsync(new OutgoingMessage(topic, "c", "{oops"u8.ToArray(), new Dictionary<string, string>()), CancellationToken.None);

        using var state = new CompactedStateConsumer<TestEvent>(TopicBase, kafka, NullLogger<CompactedStateConsumer<TestEvent>>.Instance);
        (await state.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken)).Status.ShouldBe(HealthStatus.Unhealthy);
        await state.StartAsync(TestContext.Current.CancellationToken);
        await state.WaitUntilReadyAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        state.Snapshot().Keys.Order(StringComparer.Ordinal).ShouldBe(["a", "b"]);
        state.TryGet("a", out var a).ShouldBeTrue();
        a.Value.ShouldBe(2);
        (await state.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken)).Status.ShouldBe(HealthStatus.Healthy);

        await publisher.PublishAsync(TopicBase, "d", Envelope("d", 7), CancellationToken.None);
        await publisher.PublishRawAsync(new OutgoingMessage(topic, "b", [], new Dictionary<string, string>()), CancellationToken.None);
        await WaitUntilAsync(() => state.TryGet("d", out _) && !state.TryGet("b", out _));

        await state.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task An_empty_topic_is_ready_at_once()
    {
        var kafka = Kafka.Options(redpanda.BootstrapServers, Kafka.UniqueEnvironment());
        await redpanda.CreateTopicsAsync(2, TopicName.For(TopicBase, kafka.Value.Environment).Value);

        using var state = new CompactedStateConsumer<TestEvent>(TopicBase, kafka, NullLogger<CompactedStateConsumer<TestEvent>>.Instance);
        await state.StartAsync(TestContext.Current.CancellationToken);
        await state.WaitUntilReadyAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        state.Snapshot().ShouldBeEmpty();
        await state.StopAsync(TestContext.Current.CancellationToken);
    }

    private static EventEnvelope<TestEvent> Envelope(string name, int value) =>
        EventEnvelope<TestEvent>.Create(new TestEvent(name, value), DateTimeOffset.UtcNow, "corr-state");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!condition())
        {
            DateTimeOffset.UtcNow.ShouldBeLessThan(deadline, "state did not follow the topic");
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}
