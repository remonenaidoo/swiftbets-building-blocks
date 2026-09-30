UPDATE outbox.Messages
SET AttemptCount = AttemptCount + 1,
    NextAttemptAt = @NextAttemptAt,
    LastError = @LastError,
    LeaseOwner = NULL,
    LeaseUntil = NULL
WHERE Id = @Id AND LeaseOwner = @Owner;
