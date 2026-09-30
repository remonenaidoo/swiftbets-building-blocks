namespace SwiftBets.BuildingBlocks.Core;

/// <summary>A named place where tests and fault injection can deterministically break a flow.</summary>
public interface IFaultPoint
{
    ValueTask HitAsync(string name, CancellationToken cancellationToken = default);
}
