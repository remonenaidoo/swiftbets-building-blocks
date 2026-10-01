using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace SwiftBets.BuildingBlocks.Persistence;

public static class PersistenceRegistration
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddSqlServerPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISqlConnectionFactory>(new SqlServerConnectionFactory(connectionString));
        services.TryAddSingleton<IInboxStore, SqlServerInboxStore>();
        services.AddHealthChecks().Add(new HealthCheckRegistration("sqlserver", _ => new SqlServerHealthCheck(connectionString), HealthStatus.Unhealthy, [ReadyTag], TimeSpan.FromSeconds(5)));
        return services;
    }

    public static IServiceCollection AddPostgresPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.TryAddSingleton<IInboxStore, PostgresInboxStore>();
        services.AddHealthChecks().Add(new HealthCheckRegistration("postgres", sp => new PostgresHealthCheck(sp.GetRequiredService<NpgsqlDataSource>()), HealthStatus.Unhealthy, [ReadyTag], TimeSpan.FromSeconds(5)));
        return services;
    }
}
