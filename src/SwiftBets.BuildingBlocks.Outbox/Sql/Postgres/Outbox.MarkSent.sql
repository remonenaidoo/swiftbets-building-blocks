UPDATE outbox.messages
SET sent_at = @Now, lease_owner = NULL, lease_until = NULL, last_error = NULL
WHERE id = ANY(@Ids) AND lease_owner = @Owner;
