using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Testcontainers.Redpanda;
using Xunit;

namespace SwiftBets.BuildingBlocks.Testing;

public sealed class RedpandaFixture : IAsyncLifetime
{
    private readonly RedpandaContainer _container = new RedpandaBuilder(TestImages.Redpanda).Build();

    public string BootstrapServers => _container.GetBootstrapAddress();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    public async Task CreateTopicsAsync(int partitions, params string[] topics)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();
        try
        {
            await admin.CreateTopicsAsync(topics.Select(t => new TopicSpecification { Name = t, NumPartitions = partitions, ReplicationFactor = 1 })).ConfigureAwait(false);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code is ErrorCode.NoError or ErrorCode.TopicAlreadyExists))
        {
        }
    }
}
