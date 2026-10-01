using Confluent.Kafka;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.IntegrationTests;

public sealed class PostgresOutboxTests(PostgresFixture postgres, RedpandaFixture redpanda)
{
    private const string TopicBase = "test.thing-happened.v1";

    [Fact]
    public async Task Rolled_back_transaction_leaves_no_outbox_row()
    {
        var (dataSource, kafka, _) = await SetUpAsync();
        var outbox = new PostgresOutbox(kafka, TimeProvider.System);

        await using (var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await outbox.EnqueueAsync(transaction, TopicBase, "k", NewEnvelope(), CancellationToken.None);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        (await CountAsync(dataSource, "SELECT COUNT(*) FROM outbox.messages")).ShouldBe(0);
    }

    [Fact]
    public async Task Committed_message_is_relayed_to_kafka_and_marked_sent()
    {
        var (dataSource, kafka, _) = await SetUpAsync();
        var topic = TopicName.For(TopicBase, kafka.Value.Environment).Value;
        await redpanda.CreateTopicsAsync(1, topic);
        var outbox = new PostgresOutbox(kafka, TimeProvider.System);
        var envelope = NewEnvelope();
        await using (var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await outbox.EnqueueAsync(transaction, TopicBase, "account-1", envelope, CancellationToken.None);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        using var publisher = Kafka.Publisher(kafka);
        var store = DbOutboxStore.Postgres(dataSource);
        var relay = new OutboxRelay(store, publisher, Options.Create(new OutboxOptions()), TimeProvider.System, NullLogger<OutboxRelay>.Instance);

        (await relay.RelayOnceAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await relay.RelayOnceAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
        (await store.CountPendingAsync(TestContext.Current.CancellationToken)).ShouldBe(0);

        using var consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = redpanda.BootstrapServers,
            GroupId = "outbox-" + Guid.NewGuid().ToString("N"),
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        consumer.Subscribe(topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        result.ShouldNotBeNull();
        result.Message.Key.ShouldBe("account-1");
        EnvelopeSerializer.Deserialize<TestEvent>(result.Message.Value)!.Id.ShouldBe(envelope.Id);
    }

    [Fact]
    public async Task Claimed_rows_are_skipped_by_a_second_relay_until_the_lease_ends()
    {
        var (dataSource, kafka, _) = await SetUpAsync();
        var outbox = new PostgresOutbox(kafka, TimeProvider.System);
        await using (var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await outbox.EnqueueAsync(transaction, TopicBase, "a", NewEnvelope(), CancellationToken.None);
            await outbox.EnqueueAsync(transaction, TopicBase, "b", NewEnvelope(), CancellationToken.None);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        var store = DbOutboxStore.Postgres(dataSource);
        var now = DateTimeOffset.UtcNow;
        var first = await store.ClaimAsync(10, now, "one", now.AddMinutes(1), TestContext.Current.CancellationToken);
        var second = await store.ClaimAsync(10, now, "two", now.AddMinutes(1), TestContext.Current.CancellationToken);
        var afterLease = await store.ClaimAsync(10, now.AddMinutes(2), "two", now.AddMinutes(3), TestContext.Current.CancellationToken);

        first.Select(m => m.MessageKey).ShouldBe(["a", "b"]);
        second.ShouldBeEmpty();
        afterLease.Count.ShouldBe(2);

        await store.MarkFailedAsync(afterLease[0].Id, "one", now, "stale owner", TestContext.Current.CancellationToken);
        await store.MarkFailedAsync(afterLease[0].Id, "two", now.AddMinutes(10), "broker down", TestContext.Current.CancellationToken);
        await store.MarkSentAsync([afterLease[1].Id], now, "two", TestContext.Current.CancellationToken);

        (await store.CountPendingAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await CountAsync(dataSource, "SELECT attempt_count FROM outbox.messages WHERE message_key = 'a'")).ShouldBe(1);
    }

    [Fact]
    public async Task Inbox_records_an_event_once_per_consumer()
    {
        var (dataSource, _, _) = await SetUpAsync();
        var inbox = new PostgresInboxStore(TimeProvider.System);
        var eventId = Guid.NewGuid();

        (await RecordAsync(dataSource, inbox, "notifications.email", eventId)).ShouldBeTrue();
        (await RecordAsync(dataSource, inbox, "notifications.email", eventId)).ShouldBeFalse();
        (await RecordAsync(dataSource, inbox, "audit.writer", eventId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Outbox_and_inbox_migrations_roll_back_cleanly()
    {
        var (dataSource, _, connectionString) = await SetUpAsync();

        MigrationRollback.Postgres(connectionString, OutboxRegistration.PostgresMigrations, 0).Successful.ShouldBeTrue();
        MigrationRollback.Postgres(connectionString, MigrationSource.PostgresInbox, 0).Successful.ShouldBeTrue();

        (await CountAsync(dataSource, "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name IN ('outbox', 'inbox')")).ShouldBe(0);
        MigrationRunner.RunPostgres(connectionString, false, MigrationSource.PostgresInbox, OutboxRegistration.PostgresMigrations).Successful.ShouldBeTrue();
    }

    private async Task<(NpgsqlDataSource DataSource, IOptions<KafkaOptions> Kafka, string ConnectionString)> SetUpAsync()
    {
        var name = "bb_" + Guid.NewGuid().ToString("N")[..12];
        await using (var server = new NpgsqlConnection(postgres.ConnectionString))
        {
            await server.ExecuteAsync($"CREATE DATABASE {name}");
        }

        var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = name }.ConnectionString;
        var result = MigrationRunner.RunPostgres(connectionString, ensureDatabase: false, MigrationSource.PostgresInbox, OutboxRegistration.PostgresMigrations);
        result.Successful.ShouldBeTrue(result.Error?.Message);
        return (NpgsqlDataSource.Create(connectionString), Kafka.Options(redpanda.BootstrapServers, Kafka.UniqueEnvironment()), connectionString);
    }

    private static async Task<bool> RecordAsync(NpgsqlDataSource dataSource, PostgresInboxStore inbox, string consumer, Guid eventId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var recorded = await inbox.TryRecordAsync(transaction, consumer, eventId, CancellationToken.None);
        await transaction.CommitAsync();
        return recorded;
    }

    private static async Task<int> CountAsync(NpgsqlDataSource dataSource, string sqlText)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(sqlText);
    }

    private static EventEnvelope<TestEvent> NewEnvelope() =>
        EventEnvelope<TestEvent>.Create(new TestEvent("placed", 1), DateTimeOffset.UtcNow, "corr-outbox");
}
