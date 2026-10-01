using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Identity;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class CompactedStateConsumerTests
{
    [Fact]
    public async Task An_unreachable_broker_leaves_the_state_unready_instead_of_stopping_the_host()
    {
        var options = Options.Create(new KafkaOptions { BootstrapServers = "127.0.0.1:9", Environment = "test", ClientId = "state-tests" });
        using var consumer = new CompactedStateConsumer<UserRegisteredV1>(Topics.UserRegistered, options, NullLogger<CompactedStateConsumer<UserRegisteredV1>>.Instance);

        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(12), TestContext.Current.CancellationToken);

        consumer.IsReady.ShouldBeFalse();
        consumer.ExecuteTask!.IsFaulted.ShouldBeFalse();
        await consumer.StopAsync(CancellationToken.None);
    }
}
