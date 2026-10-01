CREATE SCHEMA IF NOT EXISTS outbox;

CREATE TABLE outbox.messages
(
    sequence        bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id              uuid        NOT NULL UNIQUE,
    topic           text        NOT NULL,
    message_key     text        NOT NULL,
    event_type      text        NOT NULL,
    payload         bytea       NOT NULL,
    headers         text        NOT NULL,
    created_at      timestamptz NOT NULL,
    attempt_count   int         NOT NULL DEFAULT 0,
    next_attempt_at timestamptz NOT NULL,
    lease_owner     text        NULL,
    lease_until     timestamptz NULL,
    sent_at         timestamptz NULL,
    last_error      text        NULL
);

CREATE INDEX ix_outbox_messages_pending ON outbox.messages (next_attempt_at, sequence) WHERE sent_at IS NULL;
