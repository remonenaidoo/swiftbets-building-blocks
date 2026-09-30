IF SCHEMA_ID(N'outbox') IS NULL EXEC (N'CREATE SCHEMA outbox');

CREATE TABLE outbox.Messages
(
    Sequence      bigint IDENTITY(1, 1) NOT NULL,
    Id            uniqueidentifier      NOT NULL,
    Topic         nvarchar(249)         NOT NULL,
    MessageKey    nvarchar(200)         NOT NULL,
    EventType     nvarchar(200)         NOT NULL,
    Payload       varbinary(max)        NOT NULL,
    Headers       nvarchar(max)         NOT NULL,
    CreatedAt     datetimeoffset(3)     NOT NULL,
    AttemptCount  int                   NOT NULL CONSTRAINT DF_OutboxMessages_AttemptCount DEFAULT (0),
    NextAttemptAt datetimeoffset(3)     NOT NULL,
    LeaseOwner    nvarchar(100)         NULL,
    LeaseUntil    datetimeoffset(3)     NULL,
    SentAt        datetimeoffset(3)     NULL,
    LastError     nvarchar(2000)        NULL,
    CONSTRAINT PK_OutboxMessages PRIMARY KEY CLUSTERED (Sequence),
    CONSTRAINT UQ_OutboxMessages_Id UNIQUE (Id)
);

CREATE INDEX IX_OutboxMessages_Pending ON outbox.Messages (NextAttemptAt, Sequence) WHERE SentAt IS NULL;
