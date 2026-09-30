using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace SwiftBets.BuildingBlocks.Resilience;

public static class ResilienceRegistration
{
    public static IServiceCollection AddSwiftBetsResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline(ResiliencePipelineNames.SqlTransient, builder => builder
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(SqlTransientErrors.IsTransient)
                    .Handle<TimeoutException>(),
                MaxRetryAttempts = 4,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
            })
            .AddTimeout(TimeSpan.FromSeconds(30)));

        services.AddResiliencePipeline(ResiliencePipelineNames.KafkaProduce, builder => builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
            })
            .AddTimeout(TimeSpan.FromSeconds(10)));

        return services;
    }
}
