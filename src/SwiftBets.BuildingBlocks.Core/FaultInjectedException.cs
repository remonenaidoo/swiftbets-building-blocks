namespace SwiftBets.BuildingBlocks.Core;

public sealed class FaultInjectedException(string faultPoint)
    : Exception($"Fault injected at '{faultPoint}'.")
{
    public string FaultPoint { get; } = faultPoint;
}
