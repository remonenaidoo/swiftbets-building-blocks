using Confluent.Kafka;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.IntegrationTests;

public sealed class OutboxTests(SqlServerFixture sql, RedpandaFixture redpanda)
{
    private const string TopicBase = "test.thing-happened.v1";

    [Fact]
    public async Task Rolled_back_transaction_leaves_no_outbox_row()
    {
        var (connections, kafka) = await SetUpAsync();
        var outbox = new SqlServerOutbox(kafka, TimeProvider.System);

        await using (var connection = await connections.OpenAsync(CancellationToken.None))
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await outbox.EnqueueAsync(transaction, TopicBase, "k", NewEnvelope(), CancellationToken.None);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        (await CountAsync(connections, "SELECT COUNT(*) FROM outbox.Messages")).ShouldBe(0);
    }

    [Fact]
    public async Task Committed_message_is_relayed_to_kafka_and_marked_sent()
    {
        var (connections, kafka) = await SetUpAsync();
        var topic = TopicName.For(TopicBase, kafka.Value.Environment).Value;
        await redpanda.CreateTopicsAsync(1, topic);
        var outbox = new SqlServerOutbox(kafka, TimeProvider.System);
        var envelope = NewEnvelope();
        await using (var connection = await connections.OpenAsync(CancellationToken.None))
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await outbox.EnqueueAsync(transaction, TopicBase, "coupon-1", envelope, CancellationToken.None);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        using var publisher = Kafka.Publisher(kafka);
        var relay = new OutboxRelay(connections, publisher, Options.Create(new OutboxOptions()), TimeProvider.System, NullLogger<OutboxRelay>.Instance);

        (await relay.RelayOnceAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await relay.RelayOnceAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
        (await CountAsync(connections, "SELECT COUNT(*) FROM outbox.Messages WHERE SentAt IS NULL")).ShouldBe(0);

        using var consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = redpanda.BootstrapServers,
            GroupId = "outbox-" + Guid.NewGuid().ToString("N"),
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        consumer.Subscribe(topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        result.ShouldNotBeNull();
        result.Message.Key.ShouldBe("coupon-1");
        EnvelopeSerializer.Deserialize<TestEvent>(result.Message.Value)!.Id.ShouldBe(envelope.Id);
    }

    [Fact]
    public async Task Inbox_records_an_event_once_per_consumer()
    {
        var (connections, _) = await SetUpAsync();
        var inbox = new SqlServerInboxStore(TimeProvider.System);
        var eventId = Guid.NewGuid();

        (await RecordAsync(connections, inbox, "settlement.indexer", eventId)).ShouldBeTrue();
        (await RecordAsync(connections, inbox, "settlement.indexer", eventId)).ShouldBeFalse();
        (await RecordAsync(connections, inbox, "history.projector", eventId)).ShouldBeTrue();
    }

    private async Task<(ISqlConnectionFactory Connections, IOptions<KafkaOptions> Kafka)> SetUpAsync()
    {
        var connectionString = await sql.CreateDatabaseAsync("bb_" + Guid.NewGuid().ToString("N")[..12]);
        var result = MigrationRunner.RunSqlServer(connectionString, ensureDatabase: false, MigrationSource.Inbox, OutboxRegistration.Migrations);
        result.Successful.ShouldBeTrue(result.Error?.Message);
        return (new SqlServerConnectionFactory(connectionString), Kafka.Options(redpanda.BootstrapServers, Kafka.UniqueEnvironment()));
    }

    private static async Task<bool> RecordAsync(ISqlConnectionFactory connections, SqlServerInboxStore inbox, string consumer, Guid eventId)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        await using var transaction = await connection.BeginTransactionAsync();
        var recorded = await inbox.TryRecordAsync(transaction, consumer, eventId, CancellationToken.None);
        await transaction.CommitAsync();
        return recorded;
    }

    private static async Task<int> CountAsync(ISqlConnectionFactory connections, string sqlText)
    {
        await using var connection = await connections.OpenAsync(CancellationToken.None);
        return await connection.ExecuteScalarAsync<int>(sqlText);
    }

    private static EventEnvelope<TestEvent> NewEnvelope() =>
        EventEnvelope<TestEvent>.Create(new TestEvent("placed", 1), DateTimeOffset.UtcNow, "corr-outbox");
}
