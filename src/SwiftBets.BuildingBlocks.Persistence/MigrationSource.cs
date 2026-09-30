using System.Reflection;

namespace SwiftBets.BuildingBlocks.Persistence;

/// <summary>Scripts embedded under <c>Migrations/</c> in an assembly; lower groups run first.</summary>
public sealed record MigrationSource(Assembly Assembly, int Group)
{
    public static MigrationSource Inbox { get; } = new(typeof(MigrationSource).Assembly, 0);

    public bool Includes(string resourceName) =>
        resourceName.StartsWith(Assembly.GetName().Name + ".Migrations.", StringComparison.Ordinal)
        && resourceName.EndsWith(".sql", StringComparison.Ordinal);
}
