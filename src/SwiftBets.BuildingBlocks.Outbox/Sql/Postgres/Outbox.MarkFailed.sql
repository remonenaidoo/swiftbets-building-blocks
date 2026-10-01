UPDATE outbox.messages
SET attempt_count = attempt_count + 1,
    next_attempt_at = @NextAttemptAt,
    last_error = @LastError,
    lease_owner = NULL,
    lease_until = NULL
WHERE id = @Id AND lease_owner = @Owner;
