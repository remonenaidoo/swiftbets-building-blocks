using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace SwiftBets.BuildingBlocks.Resilience;

/// <summary>The keyed-grpc pipeline, for services whose every RPC is idempotent on a request key (the wallet).</summary>
public static class GrpcResilience
{
    public static ServiceConfig KeyedServiceConfig { get; } = new()
    {
        MethodConfigs =
        {
            new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 4,
                    InitialBackoff = TimeSpan.FromMilliseconds(200),
                    MaxBackoff = TimeSpan.FromSeconds(2),
                    BackoffMultiplier = 2,
                    RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.ResourceExhausted },
                },
            },
        },
    };

    public static IHttpClientBuilder AddKeyedGrpcResilience(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler("keyed-grpc", pipeline => pipeline
            .AddTimeout(TimeSpan.FromSeconds(10))
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
            }));
        return builder;
    }
}
