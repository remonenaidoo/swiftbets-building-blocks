namespace SwiftBets.BuildingBlocks.Core;

/// <summary>The correlation id of the unit of work in flight, flowing across HTTP, gRPC and Kafka hops.</summary>
public static class CorrelationContext
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;
    private static readonly AsyncLocal<string?> Current = new();

    public static string? CorrelationId => Current.Value;

    public static IDisposable Begin(string correlationId)
    {
        var previous = Current.Value;
        Current.Value = correlationId;
        return new Scope(previous);
    }

    public static string NewId() => Guid.CreateVersion7().ToString("N");

    public static bool IsValid(string? candidate) =>
        !string.IsNullOrEmpty(candidate)
        && candidate.Length <= MaxLength
        && candidate.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private sealed class Scope(string? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
