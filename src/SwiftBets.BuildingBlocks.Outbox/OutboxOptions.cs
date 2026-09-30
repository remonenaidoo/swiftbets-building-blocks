using System.ComponentModel.DataAnnotations;

namespace SwiftBets.BuildingBlocks.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    [Range(10, 60_000)]
    public int PollIntervalMilliseconds { get; set; } = 200;

    [Range(5, 600)]
    public int LeaseSeconds { get; set; } = 60;

    [Range(1, 3600)]
    public int MaxBackoffSeconds { get; set; } = 256;
}
