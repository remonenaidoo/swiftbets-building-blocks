INSERT INTO inbox.processed_messages (consumer, event_id, processed_at)
VALUES (@Consumer, @EventId, @ProcessedAt)
ON CONFLICT (consumer, event_id) DO NOTHING;
