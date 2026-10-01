using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace SwiftBets.BuildingBlocks.Persistence;

/// <summary>
/// Reverses applied migrations down to a target version. Every migration above the target must have a script under
/// <c>Rollbacks/</c> with the same file name; if any is missing, nothing runs. Each rollback and the removal of its
/// journal row share one transaction, newest first.
/// </summary>
public static partial class MigrationRollback
{
    public static MigrationRollbackResult SqlServer(string connectionString, MigrationSource source, int targetVersion)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        var journalExists = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.tables WHERE name = 'SchemaVersions' AND schema_id = SCHEMA_ID('dbo')") == 1;
        return Run(connection, source, targetVersion, journalExists, "SELECT ScriptName FROM dbo.SchemaVersions", "DELETE FROM dbo.SchemaVersions WHERE ScriptName = @ScriptName");
    }

    public static MigrationRollbackResult Postgres(string connectionString, MigrationSource source, int targetVersion)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        var journalExists = connection.ExecuteScalar<bool>("SELECT to_regclass('public.schemaversions') IS NOT NULL");
        return Run(connection, source, targetVersion, journalExists, "SELECT scriptname FROM public.schemaversions", "DELETE FROM public.schemaversions WHERE scriptname = @ScriptName");
    }

    /// <summary>The leading number of a migration's file name: <c>0004_reconciliation.sql</c> is version 4.</summary>
    public static int VersionOf(string resourceName)
    {
        var file = resourceName[(resourceName.LastIndexOf(".Migrations.", StringComparison.Ordinal) + ".Migrations.".Length)..];
        var match = LeadingDigits().Match(file);
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : throw new FormatException($"{resourceName} does not start with a version number.");
    }

    private static MigrationRollbackResult Run(DbConnection connection, MigrationSource source, int targetVersion, bool journalExists, string selectJournal, string deleteJournal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetVersion);
        if (!journalExists)
        {
            return MigrationRollbackResult.Succeeded([]);
        }

        var pending = connection.Query<string>(selectJournal)
            .Where(source.Includes)
            .Where(name => VersionOf(name) > targetVersion)
            .OrderByDescending(VersionOf)
            .Select(name => (Migration: name, Rollback: Read(source.Assembly, name.Replace(".Migrations.", ".Rollbacks.", StringComparison.Ordinal))))
            .ToList();

        var missing = pending.Where(p => p.Rollback is null).Select(p => p.Migration).ToList();
        if (missing.Count > 0)
        {
            return MigrationRollbackResult.Failed($"No rollback script for {string.Join(", ", missing)}; these migrations cannot be rolled back and nothing was changed.");
        }

        var rolledBack = new List<string>();
        foreach (var (migration, rollback) in pending)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var batch in Batches(rollback!))
                {
                    connection.Execute(batch, transaction: transaction, commandTimeout: 600);
                }

                connection.Execute(deleteJournal, new { ScriptName = migration }, transaction);
                transaction.Commit();
                rolledBack.Add(migration);
            }
            catch (DbException ex)
            {
                transaction.Rollback();
                return MigrationRollbackResult.Failed($"Rolling back {migration} failed: {ex.Message}", rolledBack);
            }
        }

        return MigrationRollbackResult.Succeeded(rolledBack);
    }

    private static string? Read(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        return stream is null ? null : new StreamReader(stream).ReadToEnd();
    }

    private static IEnumerable<string> Batches(string script) =>
        BatchSeparator().Split(script).Where(batch => !string.IsNullOrWhiteSpace(batch));

    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingDigits();

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex BatchSeparator();
}
