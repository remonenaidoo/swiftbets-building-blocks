namespace SwiftBets.BuildingBlocks.Core;

public sealed class FaultInjectionOptions
{
    public const string SectionName = "FaultInjection";

    public bool Enabled { get; set; }
}
