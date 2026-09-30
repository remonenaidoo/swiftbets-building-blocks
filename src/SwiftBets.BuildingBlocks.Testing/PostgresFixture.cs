using Testcontainers.PostgreSql;
using Xunit;

namespace SwiftBets.BuildingBlocks.Testing;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(TestImages.Postgres).Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);
}
