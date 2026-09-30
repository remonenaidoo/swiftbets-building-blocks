UPDATE outbox.Messages
SET SentAt = @Now, LeaseOwner = NULL, LeaseUntil = NULL, LastError = NULL
WHERE Id IN @Ids AND LeaseOwner = @Owner;
