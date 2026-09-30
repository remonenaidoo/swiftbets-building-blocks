using Prometheus;

namespace SwiftBets.BuildingBlocks.Outbox;

internal static class OutboxMetrics
{
    public static readonly Counter Published = Metrics.CreateCounter("swiftbets_outbox_published_total", "Outbox messages published.");

    public static readonly Counter Failed = Metrics.CreateCounter("swiftbets_outbox_publish_failures_total", "Outbox publish attempts that failed.");

    public static readonly Gauge Pending = Metrics.CreateGauge("swiftbets_outbox_pending", "Outbox messages not yet published.");
}
