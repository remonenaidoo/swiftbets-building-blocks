using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Messaging;

/// <summary>
/// Reads a compacted topic from the beginning on every start, without a consumer group or commits, and keeps the
/// latest envelope payload per key. An empty value is a tombstone and removes the key; an unreadable value is skipped.
/// Reports unhealthy until it has caught up, so a replica serves no stale reads after a restart.
/// </summary>
public sealed partial class CompactedStateConsumer<TPayload> : BackgroundService, ICompactedState<TPayload>, IHealthCheck
    where TPayload : IEventContract
{
    private readonly ConcurrentDictionary<string, TPayload> _values = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly KafkaOptions _options;
    private readonly ILogger _logger;

    public CompactedStateConsumer(string topicBase, IOptions<KafkaOptions> options, ILogger<CompactedStateConsumer<TPayload>> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
        Topic = TopicName.For(topicBase, _options.Environment);
    }

    public TopicName Topic { get; }

    public bool IsReady => _ready.Task.IsCompleted;

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken) => _ready.Task.WaitAsync(cancellationToken);

    public bool TryGet(string key, out TPayload value) => _values.TryGetValue(key, out value!);

    public IReadOnlyDictionary<string, TPayload> Snapshot() => new Dictionary<string, TPayload>(_values, StringComparer.Ordinal);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(IsReady
            ? HealthCheckResult.Healthy($"{Topic.Value}: {_values.Count} keys")
            : HealthCheckResult.Unhealthy($"{Topic.Value}: still reading to the end"));

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => Run(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private void Run(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = _options.ClientId,
            GroupId = $"{_options.ClientId}-state-{Guid.NewGuid():N}",
            EnableAutoCommit = false,
            EnablePartitionEof = true,
            IsolationLevel = IsolationLevel.ReadCommitted,
            AllowAutoCreateTopics = false,
        };

        using var consumer = new ConsumerBuilder<string, byte[]>(config).Build();
        var partitions = Partitions(consumer, stoppingToken);
        consumer.Assign(partitions.Select(p => new TopicPartitionOffset(p, Offset.Beginning)));
        var pending = new HashSet<TopicPartition>(partitions);
        LogStarted(Topic.Value, partitions.Count);
        MarkReadyIfCaughtUp(pending);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, byte[]>? result;
                try
                {
                    result = consumer.Consume(TimeSpan.FromMilliseconds(250));
                }
                catch (ConsumeException ex) when (!ex.Error.IsFatal)
                {
                    LogConsumeError(ex, Topic.Value);
                    continue;
                }

                if (result is null)
                {
                    continue;
                }

                if (result.IsPartitionEOF)
                {
                    if (pending.Remove(result.TopicPartition))
                    {
                        MarkReadyIfCaughtUp(pending);
                    }

                    continue;
                }

                Apply(result);
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private List<TopicPartition> Partitions(IConsumer<string, byte[]> consumer, CancellationToken stoppingToken)
    {
        using var admin = new DependentAdminClientBuilder(consumer.Handle).Build();
        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();
            // An unreachable broker at start-up is waited out like a missing topic; throwing here would stop the host.
            try
            {
                var topic = admin.GetMetadata(Topic.Value, TimeSpan.FromSeconds(10)).Topics.SingleOrDefault();
                if (topic is { Error.IsError: false, Partitions.Count: > 0 })
                {
                    return [.. topic.Partitions.Select(p => new TopicPartition(Topic.Value, p.PartitionId))];
                }

                LogTopicMissing(Topic.Value);
            }
            catch (KafkaException ex)
            {
                LogBrokerUnavailable(ex, Topic.Value);
            }

            stoppingToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(2));
        }
    }

    private void Apply(ConsumeResult<string, byte[]> result)
    {
        var key = result.Message.Key;
        if (key is null)
        {
            MessagingMetrics.Consumed.WithLabels(Topic.Value, "state_skipped").Inc();
            return;
        }

        if (result.Message.Value is null || result.Message.Value.Length == 0)
        {
            _values.TryRemove(key, out _);
            MessagingMetrics.Consumed.WithLabels(Topic.Value, "state_tombstone").Inc();
            return;
        }

        EventEnvelope<TPayload>? envelope = null;
        try
        {
            envelope = EnvelopeSerializer.Deserialize<TPayload>(result.Message.Value);
        }
        catch (JsonException)
        {
        }

        if (envelope is null || envelope.Type != TPayload.EventType || envelope.Version != TPayload.EventVersion)
        {
            LogSkipped(Topic.Value, result.Partition.Value, result.Offset.Value);
            MessagingMetrics.Consumed.WithLabels(Topic.Value, "state_skipped").Inc();
            return;
        }

        _values[key] = envelope.Payload;
        MessagingMetrics.Consumed.WithLabels(Topic.Value, "state_applied").Inc();
    }

    private void MarkReadyIfCaughtUp(HashSet<TopicPartition> pending)
    {
        if (pending.Count == 0 && _ready.TrySetResult())
        {
            LogReady(Topic.Value, _values.Count);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reading state from {Topic} across {Partitions} partitions")]
    private partial void LogStarted(string topic, int partitions);

    [LoggerMessage(Level = LogLevel.Information, Message = "State from {Topic} is current with {Keys} keys")]
    private partial void LogReady(string topic, int keys);

    [LoggerMessage(Level = LogLevel.Warning, Message = "State topic {Topic} has no partitions yet; retrying")]
    private partial void LogTopicMissing(string topic);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Broker unavailable while reading state topic {Topic}; retrying")]
    private partial void LogBrokerUnavailable(Exception exception, string topic);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consume error on state topic {Topic}")]
    private partial void LogConsumeError(Exception exception, string topic);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped unreadable state record {Topic}[{Partition}]@{Offset}")]
    private partial void LogSkipped(string topic, int partition, long offset);
}
