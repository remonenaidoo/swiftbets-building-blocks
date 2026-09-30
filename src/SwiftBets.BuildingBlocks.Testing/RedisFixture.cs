using Testcontainers.Redis;
using Xunit;

namespace SwiftBets.BuildingBlocks.Testing;

public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder(TestImages.Redis).WithCommand("--maxmemory-policy", "noeviction").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);
}
