SELECT COUNT(*) FROM outbox.messages WHERE sent_at IS NULL;
