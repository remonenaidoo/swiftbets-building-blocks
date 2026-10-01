using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Persistence;

namespace SwiftBets.BuildingBlocks.Outbox;

public static class OutboxRegistration
{
    public static MigrationSource Migrations { get; } = new(typeof(OutboxRegistration).Assembly, 0);

    public static MigrationSource PostgresMigrations { get; } = new(typeof(OutboxRegistration).Assembly, 0, "Postgres");

    /// <summary>Registers the outbox writer and the relay; requires an <see cref="ISqlConnectionFactory"/> and Kafka messaging.</summary>
    public static IServiceCollection AddSqlServerOutbox(this IServiceCollection services, IConfiguration configuration, bool runRelay = true)
    {
        services.TryAddSingleton<IOutbox, SqlServerOutbox>();
        services.TryAddSingleton<IOutboxStore>(sp => DbOutboxStore.SqlServer(sp.GetRequiredService<ISqlConnectionFactory>()));
        return services.AddRelay(configuration, runRelay);
    }

    /// <summary>Registers the outbox writer and the relay; requires Postgres persistence and Kafka messaging.</summary>
    public static IServiceCollection AddPostgresOutbox(this IServiceCollection services, IConfiguration configuration, bool runRelay = true)
    {
        services.TryAddSingleton<IOutbox, PostgresOutbox>();
        services.TryAddSingleton<IOutboxStore>(sp => DbOutboxStore.Postgres(sp.GetRequiredService<NpgsqlDataSource>()));
        return services.AddRelay(configuration, runRelay);
    }

    private static IServiceCollection AddRelay(this IServiceCollection services, IConfiguration configuration, bool runRelay)
    {
        services.AddValidatedOptions<OutboxOptions>(configuration, OutboxOptions.SectionName);
        services.TryAddSingleton(TimeProvider.System);
        if (runRelay)
        {
            services.AddSingleton<OutboxRelay>();
            services.AddHostedService(sp => sp.GetRequiredService<OutboxRelay>());
        }

        return services;
    }
}
