using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace SwiftBets.BuildingBlocks.Messaging;

public sealed class KafkaHealthCheck(IOptions<KafkaOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = options.Value.BootstrapServers }).Build();
            var metadata = admin.GetMetadata(TimeSpan.FromSeconds(3));
            return Task.FromResult(metadata.Brokers.Count > 0 ? HealthCheckResult.Healthy() : new HealthCheckResult(context.Registration.FailureStatus, "No brokers"));
        }
        catch (KafkaException ex)
        {
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, "Kafka unreachable", ex));
        }
    }
}
