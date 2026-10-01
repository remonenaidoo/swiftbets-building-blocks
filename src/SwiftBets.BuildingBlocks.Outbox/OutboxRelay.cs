using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Messaging;

namespace SwiftBets.BuildingBlocks.Outbox;

/// <summary>
/// Claims pending outbox rows under a lease (several relays can run safely), publishes them in sequence order and marks
/// them sent. A failed message blocks later messages with the same key in the batch so per-key order holds; rows are
/// never dropped.
/// </summary>
public sealed partial class OutboxRelay(
    IOutboxStore store,
    IEventPublisher publisher,
    IOptions<OutboxOptions> options,
    TimeProvider time,
    ILogger<OutboxRelay> logger) : BackgroundService
{
    private readonly string _owner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromMilliseconds(options.Value.PollIntervalMilliseconds);
        var lastBacklogCheck = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            var published = 0;
            try
            {
                published = await RelayOnceAsync(stoppingToken).ConfigureAwait(false);
                if (time.GetUtcNow() - lastBacklogCheck > TimeSpan.FromSeconds(5))
                {
                    await UpdateBacklogAsync(stoppingToken).ConfigureAwait(false);
                    lastBacklogCheck = time.GetUtcNow();
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogRelayFailed(ex);
            }

            if (published == 0)
            {
                try
                {
                    await Task.Delay(pollInterval, time, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public async Task<int> RelayOnceAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var claimed = await store.ClaimAsync(options.Value.BatchSize, now, _owner, now.AddSeconds(options.Value.LeaseSeconds), cancellationToken).ConfigureAwait(false);

        if (claimed.Count == 0)
        {
            return 0;
        }

        var sent = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        await Parallel.ForEachAsync(
            claimed.GroupBy(m => m.MessageKey, StringComparer.Ordinal),
            new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = cancellationToken },
            async (group, token) =>
            {
                foreach (var message in group)
                {
                    try
                    {
                        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(message.Headers) ?? [];
                        await publisher.PublishRawAsync(new OutgoingMessage(message.Topic, message.MessageKey, message.Payload, headers), token).ConfigureAwait(false);
                        sent.Add(message.Id);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        OutboxMetrics.Failed.Inc();
                        LogPublishFailed(ex, message.Id, message.Topic, message.AttemptCount + 1);
                        await MarkFailedAsync(message, ex).ConfigureAwait(false);
                        return;
                    }
                }
            }).ConfigureAwait(false);

        if (!sent.IsEmpty)
        {
            await store.MarkSentAsync([.. sent], time.GetUtcNow(), _owner, CancellationToken.None).ConfigureAwait(false);
            OutboxMetrics.Published.Inc(sent.Count);
        }

        return sent.Count;
    }

    private Task MarkFailedAsync(OutboxMessage message, Exception exception)
    {
        var backoff = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, message.AttemptCount + 1), options.Value.MaxBackoffSeconds));
        var error = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
        return store.MarkFailedAsync(message.Id, _owner, time.GetUtcNow() + backoff, error, CancellationToken.None);
    }

    private async Task UpdateBacklogAsync(CancellationToken cancellationToken) =>
        OutboxMetrics.Pending.Set(await store.CountPendingAsync(cancellationToken).ConfigureAwait(false));

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox relay iteration failed")]
    private partial void LogRelayFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} to {Topic} failed on attempt {Attempt}")]
    private partial void LogPublishFailed(Exception exception, Guid messageId, string topic, int attempt);
}
