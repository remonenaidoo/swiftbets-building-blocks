using System.Reflection;

namespace SwiftBets.BuildingBlocks.Persistence;

/// <summary>
/// Scripts embedded under <c>Migrations/</c> (or <c>{Folder}/Migrations/</c>) in an assembly; lower groups run first.
/// Their reversals live in the sibling <c>Rollbacks/</c> folder.
/// </summary>
public sealed record MigrationSource(Assembly Assembly, int Group, string? Folder = null)
{
    public static MigrationSource Inbox { get; } = new(typeof(MigrationSource).Assembly, 0);

    public static MigrationSource PostgresInbox { get; } = new(typeof(MigrationSource).Assembly, 0, "Postgres");

    private string Prefix => $"{Assembly.GetName().Name}.{(Folder is null ? string.Empty : Folder + ".")}Migrations.";

    public bool Includes(string resourceName) =>
        resourceName.StartsWith(Prefix, StringComparison.Ordinal)
        && resourceName.EndsWith(".sql", StringComparison.Ordinal);
}
