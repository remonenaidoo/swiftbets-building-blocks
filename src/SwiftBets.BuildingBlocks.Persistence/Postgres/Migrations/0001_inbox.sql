CREATE SCHEMA IF NOT EXISTS inbox;

CREATE TABLE inbox.processed_messages
(
    consumer     text        NOT NULL,
    event_id     uuid        NOT NULL,
    processed_at timestamptz NOT NULL,
    PRIMARY KEY (consumer, event_id)
);
