using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Resilience;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Messaging;

public static class MessagingRegistration
{
    public static IServiceCollection AddKafkaMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<KafkaOptions>(configuration, KafkaOptions.SectionName);
        services.AddSwiftBetsResilience();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddHealthChecks().AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
        return services;
    }

    public static IServiceCollection AddKafkaConsumer<TPayload, THandler>(this IServiceCollection services, string topicBase, string groupId)
        where TPayload : IEventContract
        where THandler : class, IEventHandler<TPayload>
    {
        services.AddScoped<IEventHandler<TPayload>, THandler>();
        services.AddSingleton<IHostedService>(sp => new KafkaConsumerHost<TPayload>(
            new ConsumerRegistration(topicBase, groupId),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IEventPublisher>(),
            sp.GetRequiredService<IOptions<KafkaOptions>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<KafkaConsumerHost<TPayload>>>()));
        return services;
    }
}
