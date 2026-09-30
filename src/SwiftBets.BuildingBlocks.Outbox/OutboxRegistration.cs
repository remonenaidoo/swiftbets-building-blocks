using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Persistence;

namespace SwiftBets.BuildingBlocks.Outbox;

public static class OutboxRegistration
{
    public static MigrationSource Migrations { get; } = new(typeof(OutboxRegistration).Assembly, 0);

    /// <summary>Registers the outbox writer and the relay; requires an <see cref="ISqlConnectionFactory"/> and Kafka messaging.</summary>
    public static IServiceCollection AddSqlServerOutbox(this IServiceCollection services, IConfiguration configuration, bool runRelay = true)
    {
        services.AddValidatedOptions<OutboxOptions>(configuration, OutboxOptions.SectionName);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOutbox, SqlServerOutbox>();
        if (runRelay)
        {
            services.AddSingleton<OutboxRelay>();
            services.AddHostedService(sp => sp.GetRequiredService<OutboxRelay>());
        }

        return services;
    }
}
