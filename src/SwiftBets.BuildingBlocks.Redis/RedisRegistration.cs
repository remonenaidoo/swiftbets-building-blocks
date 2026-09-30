using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace SwiftBets.BuildingBlocks.Redis;

public static class RedisRegistration
{
    /// <summary>Connects lazily and keeps retrying in the background, so a Redis blip degrades readiness instead of crashing the host.</summary>
    public static IServiceCollection AddSwiftBetsRedis(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
        return services;
    }
}
