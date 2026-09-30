WITH next AS
(
    SELECT TOP (@BatchSize) *
    FROM outbox.Messages WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE SentAt IS NULL
      AND NextAttemptAt <= @Now
      AND (LeaseUntil IS NULL OR LeaseUntil < @Now)
    ORDER BY Sequence
)
UPDATE next
SET LeaseOwner = @Owner, LeaseUntil = @LeaseUntil
OUTPUT inserted.Sequence, inserted.Id, inserted.Topic, inserted.MessageKey, inserted.Payload, inserted.Headers, inserted.AttemptCount;
