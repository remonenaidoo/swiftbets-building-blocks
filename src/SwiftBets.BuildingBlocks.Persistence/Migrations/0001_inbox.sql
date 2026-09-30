IF SCHEMA_ID(N'inbox') IS NULL EXEC (N'CREATE SCHEMA inbox');

CREATE TABLE inbox.ProcessedMessages
(
    Consumer    nvarchar(200)    NOT NULL,
    EventId     uniqueidentifier NOT NULL,
    ProcessedAt datetimeoffset(3) NOT NULL,
    CONSTRAINT PK_ProcessedMessages PRIMARY KEY CLUSTERED (Consumer, EventId)
);
