using Microsoft.Extensions.Options;
using Polly.Registry;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace SwiftBets.BuildingBlocks.IntegrationTests;

internal static class Kafka
{
    public static IOptions<KafkaOptions> Options(string bootstrapServers, string environment) =>
        Microsoft.Extensions.Options.Options.Create(new KafkaOptions { BootstrapServers = bootstrapServers, Environment = environment, ClientId = "tests" });

    public static KafkaEventPublisher Publisher(IOptions<KafkaOptions> options) =>
        new(options, new ServiceCollection().AddSwiftBetsResilience().BuildServiceProvider().GetRequiredService<ResiliencePipelineProvider<string>>());

    public static string UniqueEnvironment() => "t" + Guid.NewGuid().ToString("N")[..10];
}
