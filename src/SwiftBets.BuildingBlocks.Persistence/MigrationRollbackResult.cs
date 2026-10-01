namespace SwiftBets.BuildingBlocks.Persistence;

/// <summary>Which migrations were rolled back, newest first, and why the run stopped if it did.</summary>
public sealed record MigrationRollbackResult(bool Successful, IReadOnlyList<string> RolledBack, string? Error)
{
    public static MigrationRollbackResult Succeeded(IReadOnlyList<string> rolledBack) => new(true, rolledBack, null);

    public static MigrationRollbackResult Failed(string error, IReadOnlyList<string>? rolledBack = null) => new(false, rolledBack ?? [], error);
}
