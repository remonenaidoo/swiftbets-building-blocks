using System.ComponentModel.DataAnnotations;

namespace SwiftBets.BuildingBlocks.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    [Required]
    public string BootstrapServers { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[a-z0-9]+$")]
    public string Environment { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Range(1, 3600)]
    public int MaxTransientBackoffSeconds { get; set; } = 60;
}
