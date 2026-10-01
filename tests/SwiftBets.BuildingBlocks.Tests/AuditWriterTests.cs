using System.Data.Common;
using Microsoft.Extensions.Time.Testing;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.Contracts.Audit;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class AuditWriterTests
{
    private readonly RecordingOutbox outbox = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Entry_goes_to_the_audit_topic_keyed_by_subject_with_json_snapshots()
    {
        var writer = new AuditWriter(outbox, time, "compliance");

        await writer.RecordAsync(null!, new AuditEntry("operator7", "limit.set", "user", "u-1", new { amount = 100 }, new { amount = 50 }), CancellationToken.None);

        var (topic, key, recorded) = outbox.Enqueued.ShouldHaveSingleItem();
        topic.ShouldBe(Topics.AuditRecorded);
        key.ShouldBe("user:u-1");
        (recorded.Service, recorded.Actor, recorded.Action, recorded.SubjectId).ShouldBe(("compliance", "operator7", "limit.set", "u-1"));
        recorded.Before.ShouldBe("""{"amount":100}""");
        recorded.After.ShouldBe("""{"amount":50}""");
        recorded.OccurredAt.ShouldBe(time.GetUtcNow());
        recorded.CorrelationId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Json_strings_pass_through_and_missing_snapshots_stay_null()
    {
        var writer = new AuditWriter(outbox, time, "identity");

        await writer.RecordAsync(null!, new AuditEntry("self", "session.revoked", "session", "s-1", Before: """{"device":"phone"}"""), CancellationToken.None);

        var recorded = outbox.Enqueued.ShouldHaveSingleItem().Recorded;
        recorded.Before.ShouldBe("""{"device":"phone"}""");
        recorded.After.ShouldBeNull();
    }

    private sealed class RecordingOutbox : IOutbox
    {
        public List<(string Topic, string Key, AuditRecordedV1 Recorded)> Enqueued { get; } = [];

        public Task EnqueueAsync<TPayload>(DbTransaction transaction, string topicBase, string key, EventEnvelope<TPayload> envelope, CancellationToken cancellationToken)
            where TPayload : IEventContract
        {
            Enqueued.Add((topicBase, key, (AuditRecordedV1)(object)envelope.Payload));
            return Task.CompletedTask;
        }
    }
}
