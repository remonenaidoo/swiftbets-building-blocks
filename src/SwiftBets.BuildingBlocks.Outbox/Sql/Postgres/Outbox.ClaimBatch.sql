WITH next AS
(
    SELECT sequence
    FROM outbox.messages
    WHERE sent_at IS NULL
      AND next_attempt_at <= @Now
      AND (lease_until IS NULL OR lease_until < @Now)
    ORDER BY sequence
    LIMIT @BatchSize
    FOR UPDATE SKIP LOCKED
)
UPDATE outbox.messages m
SET lease_owner = @Owner, lease_until = @LeaseUntil
FROM next
WHERE m.sequence = next.sequence
RETURNING m.sequence AS "Sequence", m.id AS "Id", m.topic AS "Topic", m.message_key AS "MessageKey",
          m.payload AS "Payload", m.headers AS "Headers", m.attempt_count AS "AttemptCount";
