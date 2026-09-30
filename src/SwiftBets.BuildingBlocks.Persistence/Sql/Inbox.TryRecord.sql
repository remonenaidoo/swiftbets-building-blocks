INSERT INTO inbox.ProcessedMessages (Consumer, EventId, ProcessedAt)
SELECT @Consumer, @EventId, @ProcessedAt
WHERE NOT EXISTS
(
    SELECT 1 FROM inbox.ProcessedMessages WITH (UPDLOCK, HOLDLOCK)
    WHERE Consumer = @Consumer AND EventId = @EventId
);
