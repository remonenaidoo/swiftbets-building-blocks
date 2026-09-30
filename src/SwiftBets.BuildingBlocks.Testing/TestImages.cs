namespace SwiftBets.BuildingBlocks.Testing;

/// <summary>The exact infrastructure versions the platform runs (DECISIONS.md D37).</summary>
public static class TestImages
{
    public const string Redpanda = "docker.redpanda.com/redpandadata/redpanda:v26.1.18";
    public const string SqlServer = "mcr.microsoft.com/mssql/server:2025-CU9-ubuntu-24.04";
    public const string Postgres = "pgvector/pgvector:0.8.6-pg17-trixie";
    public const string Redis = "redis:8.6-alpine";
}
