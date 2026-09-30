namespace SwiftBets.BuildingBlocks.Resilience;

public static class ResiliencePipelineNames
{
    public const string SqlTransient = "sql-transient";
    public const string KafkaProduce = "kafka-produce";
}
