using System.Collections.Concurrent;
using System.Reflection;

namespace SwiftBets.BuildingBlocks.Persistence;

/// <summary>
/// Loads SQL embedded under a <c>Sql/</c> folder of an assembly. A file <c>Sql/Outbox.ClaimBatch.sql</c> is fetched as
/// <c>Get("Outbox.ClaimBatch")</c>.
/// </summary>
public sealed class SqlResources
{
    private static readonly ConcurrentDictionary<Assembly, SqlResources> Cache = new();
    private readonly Dictionary<string, string> _statements;

    private SqlResources(Assembly assembly)
    {
        _statements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            var marker = resource.IndexOf(".Sql.", StringComparison.Ordinal);
            if (marker < 0 || !resource.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            var name = resource[(marker + 5)..^4];
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            _statements[name] = reader.ReadToEnd();
        }
    }

    public static SqlResources For<TMarker>() => Cache.GetOrAdd(typeof(TMarker).Assembly, a => new SqlResources(a));

    public string Get(string name) =>
        _statements.TryGetValue(name, out var sql)
            ? sql
            : throw new KeyNotFoundException($"No embedded SQL named '{name}'. Known: {string.Join(", ", _statements.Keys)}");

    public IReadOnlyCollection<string> Names => _statements.Keys;
}
