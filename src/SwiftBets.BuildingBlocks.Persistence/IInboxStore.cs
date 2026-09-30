using System.Data.Common;

namespace SwiftBets.BuildingBlocks.Persistence;

public interface IInboxStore
{
    /// <summary>Records the event inside the caller's transaction; false means it was already processed.</summary>
    Task<bool> TryRecordAsync(DbTransaction transaction, string consumer, Guid eventId, CancellationToken cancellationToken);
}
