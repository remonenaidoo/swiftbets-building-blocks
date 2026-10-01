INSERT INTO outbox.messages (id, topic, message_key, event_type, payload, headers, created_at, next_attempt_at)
VALUES (@Id, @Topic, @MessageKey, @EventType, @Payload, @Headers, @CreatedAt, @CreatedAt);
