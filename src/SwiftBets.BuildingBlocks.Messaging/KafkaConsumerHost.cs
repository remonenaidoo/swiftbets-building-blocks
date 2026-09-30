using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Messaging;

/// <summary>
/// Consumes one topic: validates, hands the event to its handler in a DI scope, and commits the offset only after
/// success or after the message has been parked on the topic's dead-letter queue.
/// </summary>
public sealed partial class KafkaConsumerHost<TPayload> : BackgroundService
    where TPayload : IEventContract
{
    public static readonly ActivitySource ActivitySource = new("SwiftBets.Messaging");

    private readonly ConsumerRegistration _registration;
    private readonly IServiceScopeFactory _scopes;
    private readonly IEventPublisher _publisher;
    private readonly KafkaOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly Dictionary<TopicPartition, DateTimeOffset> _pausedUntil = [];
    private readonly Dictionary<TopicPartitionOffset, int> _transientAttempts = [];

    public KafkaConsumerHost(
        ConsumerRegistration registration,
        IServiceScopeFactory scopes,
        IEventPublisher publisher,
        IOptions<KafkaOptions> options,
        TimeProvider time,
        ILogger<KafkaConsumerHost<TPayload>> logger)
    {
        _registration = registration;
        _scopes = scopes;
        _publisher = publisher;
        _options = options.Value;
        _time = time;
        _logger = logger;
        Topic = TopicName.For(registration.TopicBase, _options.Environment);
    }

    public TopicName Topic { get; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => RunAsync(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = _options.ClientId,
            GroupId = _registration.GroupId,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            IsolationLevel = IsolationLevel.ReadCommitted,
            AllowAutoCreateTopics = false,
        };

        using var consumer = new ConsumerBuilder<string, byte[]>(config)
            .SetPartitionsRevokedHandler((_, revoked) =>
            {
                foreach (var partition in revoked)
                {
                    _pausedUntil.Remove(partition.TopicPartition);
                }
            })
            .Build();
        consumer.Subscribe(Topic.Value);
        LogStarted(Topic.Value, _registration.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ResumeDuePartitions(consumer);
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

                if (result is null || result.IsPartitionEOF)
                {
                    continue;
                }

                var hold = await ProcessAsync(result, stoppingToken).ConfigureAwait(false);
                if (hold is { } until)
                {
                    consumer.Pause([result.TopicPartition]);
                    consumer.Seek(result.TopicPartitionOffset);
                    _pausedUntil[result.TopicPartition] = until;
                }
                else
                {
                    consumer.Commit(result);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    /// <summary>Returns a resume time when the message must be redelivered later, or null when its offset may be committed.</summary>
    private async Task<DateTimeOffset?> ProcessAsync(ConsumeResult<string, byte[]> result, CancellationToken stoppingToken)
    {
        var headers = ReadHeaders(result.Message.Headers);
        var now = _time.GetUtcNow();
        if (DeferredDelivery.DueAt(headers) is { } due && due > now)
        {
            MessagingMetrics.Consumed.WithLabels(Topic.Value, "deferred").Inc();
            return due;
        }

        EventEnvelope<TPayload>? envelope;
        try
        {
            envelope = EnvelopeSerializer.Deserialize<TPayload>(result.Message.Value);
        }
        catch (JsonException ex)
        {
            await DeadLetterAsync(result, headers, $"deserialization: {ex.Message}", stoppingToken).ConfigureAwait(false);
            return null;
        }

        using var scope = _scopes.CreateAsyncScope();
        var rejection = await ValidateAsync(envelope, scope.ServiceProvider, stoppingToken).ConfigureAwait(false);
        if (rejection is not null)
        {
            await DeadLetterAsync(result, headers, rejection, stoppingToken).ConfigureAwait(false);
            return null;
        }

        using var correlation = CorrelationContext.Begin(envelope!.CorrelationId);
        headers.TryGetValue(MessageHeaders.TraceParent, out var traceParent);
        using var activity = ActivitySource.StartActivity($"consume {Topic.Value}", ActivityKind.Consumer, traceParent);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", Topic.Value);
        activity?.SetTag("messaging.message.id", envelope.Id.ToString());

        var started = Stopwatch.GetTimestamp();
        try
        {
            var handler = scope.ServiceProvider.GetRequiredService<IEventHandler<TPayload>>();
            await handler.HandleAsync(
                new ConsumedEvent<TPayload>(envelope, result.Topic, result.Partition.Value, result.Offset.Value, headers),
                stoppingToken).ConfigureAwait(false);
            _transientAttempts.Remove(result.TopicPartitionOffset);
            MessagingMetrics.HandlerDuration.WithLabels(Topic.Value).Observe(Stopwatch.GetElapsedTime(started).TotalSeconds);
            MessagingMetrics.Consumed.WithLabels(Topic.Value, "handled").Inc();
            return null;
        }
        catch (PoisonMessageException ex)
        {
            await DeadLetterAsync(result, headers, ex.Message, stoppingToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            var attempt = _transientAttempts.GetValueOrDefault(result.TopicPartitionOffset) + 1;
            _transientAttempts[result.TopicPartitionOffset] = attempt;
            var backoff = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt - 1), _options.MaxTransientBackoffSeconds));
            LogTransientFailure(ex, Topic.Value, result.Partition.Value, result.Offset.Value, attempt, backoff);
            MessagingMetrics.Consumed.WithLabels(Topic.Value, "transient_failure").Inc();
            return now + backoff;
        }
    }

    private static async Task<string?> ValidateAsync(EventEnvelope<TPayload>? envelope, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (envelope is null)
        {
            return "envelope: empty message";
        }

        if (envelope.Type != TPayload.EventType || envelope.Version != TPayload.EventVersion)
        {
            return $"envelope: expected {TPayload.EventType} v{TPayload.EventVersion}, got {envelope.Type} v{envelope.Version}";
        }

        if (services.GetService<IValidator<TPayload>>() is { } validator)
        {
            var validation = await validator.ValidateAsync(envelope.Payload, cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return "validation: " + string.Join("; ", validation.Errors.Select(e => $"{e.PropertyName} {e.ErrorMessage}"));
            }
        }

        return null;
    }

    private async Task DeadLetterAsync(ConsumeResult<string, byte[]> result, Dictionary<string, string> headers, string reason, CancellationToken cancellationToken)
    {
        headers[MessageHeaders.DeadLetterSourceTopic] = result.Topic;
        headers[MessageHeaders.DeadLetterSourcePartition] = result.Partition.Value.ToString(CultureInfo.InvariantCulture);
        headers[MessageHeaders.DeadLetterSourceOffset] = result.Offset.Value.ToString(CultureInfo.InvariantCulture);
        headers[MessageHeaders.DeadLetterReason] = reason.Length > 1000 ? reason[..1000] : reason;
        await _publisher.PublishRawAsync(
            new OutgoingMessage(Topic.DeadLetter().Value, result.Message.Key ?? string.Empty, result.Message.Value ?? [], headers),
            cancellationToken).ConfigureAwait(false);
        MessagingMetrics.Consumed.WithLabels(Topic.Value, "dead_lettered").Inc();
        LogDeadLettered(Topic.Value, result.Partition.Value, result.Offset.Value, reason);
    }

    private void ResumeDuePartitions(IConsumer<string, byte[]> consumer)
    {
        if (_pausedUntil.Count == 0)
        {
            return;
        }

        var now = _time.GetUtcNow();
        var due = _pausedUntil.Where(p => p.Value <= now).Select(p => p.Key).ToList();
        if (due.Count > 0)
        {
            consumer.Resume(due);
            foreach (var partition in due)
            {
                _pausedUntil.Remove(partition);
            }
        }
    }

    private static Dictionary<string, string> ReadHeaders(Headers? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (headers is null)
        {
            return result;
        }

        foreach (var header in headers)
        {
            result[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consuming {Topic} as {GroupId}")]
    private partial void LogStarted(string topic, string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consume error on {Topic}")]
    private partial void LogConsumeError(Exception exception, string topic);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transient failure on {Topic}[{Partition}]@{Offset}, attempt {Attempt}; redelivering in {Backoff}")]
    private partial void LogTransientFailure(Exception exception, string topic, int partition, long offset, int attempt, TimeSpan backoff);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dead-lettered {Topic}[{Partition}]@{Offset}: {Reason}")]
    private partial void LogDeadLettered(string topic, int partition, long offset, string reason);
}
