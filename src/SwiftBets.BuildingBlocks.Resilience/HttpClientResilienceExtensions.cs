using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace SwiftBets.BuildingBlocks.Resilience;

public static class HttpClientResilienceExtensions
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>Retries only safe methods (GET, HEAD, OPTIONS...); unsafe calls get the breaker and timeouts but no retry.</summary>
    public static IHttpClientBuilder AddIdempotentResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
        return builder;
    }

    /// <summary>Retries safe methods, and unsafe methods only when the request carries an Idempotency-Key.</summary>
    public static IHttpClientBuilder AddKeyedResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            var transient = options.Retry.ShouldHandle;
            options.Retry.ShouldHandle = async args =>
            {
                var request = args.Context.GetRequestMessage();
                return await transient(args).ConfigureAwait(false) && request is not null && IsRetrySafe(request);
            };
        });
        return builder;
    }

    public static bool IsRetrySafe(HttpRequestMessage request) =>
        request.Method == HttpMethod.Get
        || request.Method == HttpMethod.Head
        || request.Method == HttpMethod.Options
        || request.Headers.Contains(IdempotencyKeyHeader);
}
