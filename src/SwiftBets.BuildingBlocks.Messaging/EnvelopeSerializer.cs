using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.BuildingBlocks.Messaging;

public static class EnvelopeSerializer
{
    public static byte[] Serialize<TPayload>(EventEnvelope<TPayload> envelope)
        where TPayload : IEventContract =>
        JsonSerializer.SerializeToUtf8Bytes(envelope, ContractJson.Options);

    public static EventEnvelope<TPayload>? Deserialize<TPayload>(ReadOnlySpan<byte> value)
        where TPayload : IEventContract =>
        JsonSerializer.Deserialize<EventEnvelope<TPayload>>(value, ContractJson.Options);

    public static Dictionary<string, string> Headers<TPayload>(EventEnvelope<TPayload> envelope)
        where TPayload : IEventContract
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessageHeaders.EventId] = envelope.Id.ToString(),
            [MessageHeaders.EventType] = envelope.Type,
            [MessageHeaders.EventVersion] = envelope.Version.ToString(CultureInfo.InvariantCulture),
            [MessageHeaders.CorrelationId] = envelope.CorrelationId,
        };
        if (envelope.CausationId is not null)
        {
            headers[MessageHeaders.CausationId] = envelope.CausationId;
        }

        if (Activity.Current?.Id is { } traceParent)
        {
            headers[MessageHeaders.TraceParent] = traceParent;
        }

        return headers;
    }
}
