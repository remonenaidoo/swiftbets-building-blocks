using System.Data.Common;
using System.Text.Json;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.Contracts.Audit;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.BuildingBlocks.Outbox;

/// <summary>One audited change. Actor is a user id or a service name; Before and After are any JSON-serialisable snapshot.</summary>
public sealed record AuditEntry(string Actor, string Action, string SubjectType, string SubjectId, object? Before = null, object? After = null);

public interface IAuditWriter
{
    /// <summary>Records the entry in the caller's transaction, so the audit trail holds exactly the changes that committed.</summary>
    Task RecordAsync(DbTransaction transaction, AuditEntry entry, CancellationToken cancellationToken);
}

/// <summary>Publishes <see cref="AuditRecordedV1"/> through the outbox, keyed by subject so each subject's trail stays in order.</summary>
public sealed class AuditWriter(IOutbox outbox, TimeProvider time, string service) : IAuditWriter
{
    public Task RecordAsync(DbTransaction transaction, AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var now = time.GetUtcNow();
        var correlationId = CorrelationContext.CorrelationId ?? CorrelationContext.NewId();
        var recorded = new AuditRecordedV1(
            Guid.CreateVersion7(now), service, entry.Actor, entry.Action, entry.SubjectType, entry.SubjectId,
            Snapshot(entry.Before), Snapshot(entry.After), correlationId, now);
        return outbox.EnqueueAsync(transaction, Topics.AuditRecorded, $"{entry.SubjectType}:{entry.SubjectId}",
            EventEnvelope<AuditRecordedV1>.Create(recorded, now, correlationId), cancellationToken);
    }

    private static string? Snapshot(object? value) =>
        value switch
        {
            null => null,
            string json => json,
            _ => JsonSerializer.Serialize(value, value.GetType(), ContractJson.Options),
        };
}
