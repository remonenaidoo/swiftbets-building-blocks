using System.Globalization;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Messaging;

/// <summary>Messages carrying a future retry-due-at header are held (partition paused) until due, never slept on.</summary>
public static class DeferredDelivery
{
    public static DateTimeOffset? DueAt(IReadOnlyDictionary<string, string> headers) =>
        headers.TryGetValue(MessageHeaders.RetryDueAt, out var raw)
        && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var due)
            ? due
            : null;

    public static string Format(DateTimeOffset dueAt) => dueAt.ToString("O", CultureInfo.InvariantCulture);
}
