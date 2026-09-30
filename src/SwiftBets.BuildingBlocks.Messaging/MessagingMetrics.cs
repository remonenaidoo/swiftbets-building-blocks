using Prometheus;

namespace SwiftBets.BuildingBlocks.Messaging;

internal static class MessagingMetrics
{
    public static readonly Counter Consumed = Metrics.CreateCounter(
        "swiftbets_messages_consumed_total",
        "Messages consumed, by outcome.",
        new CounterConfiguration { LabelNames = ["topic", "outcome"] });

    public static readonly Histogram HandlerDuration = Metrics.CreateHistogram(
        "swiftbets_message_handler_duration_seconds",
        "Handler duration for successfully handled messages.",
        new HistogramConfiguration { LabelNames = ["topic"], Buckets = Histogram.ExponentialBuckets(0.001, 2, 14) });
}
