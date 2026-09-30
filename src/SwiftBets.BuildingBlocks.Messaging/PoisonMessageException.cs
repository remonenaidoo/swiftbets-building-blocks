namespace SwiftBets.BuildingBlocks.Messaging;

public sealed class PoisonMessageException : Exception
{
    public PoisonMessageException(string reason)
        : base(reason)
    {
    }

    public PoisonMessageException(string reason, Exception innerException)
        : base(reason, innerException)
    {
    }
}
