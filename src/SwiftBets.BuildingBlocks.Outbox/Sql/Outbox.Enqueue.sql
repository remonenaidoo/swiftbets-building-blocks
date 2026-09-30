INSERT INTO outbox.Messages (Id, Topic, MessageKey, EventType, Payload, Headers, CreatedAt, NextAttemptAt)
VALUES (@Id, @Topic, @MessageKey, @EventType, @Payload, @Headers, @CreatedAt, @CreatedAt);
