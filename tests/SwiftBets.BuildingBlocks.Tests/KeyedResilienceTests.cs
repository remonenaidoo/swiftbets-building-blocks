using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.BuildingBlocks.Resilience;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class KeyedResilienceTests
{
    [Fact]
    public async Task Unsafe_request_with_an_idempotency_key_is_retried()
    {
        var (client, handler) = Build();
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://wallet/credits");
        request.Headers.Add(HttpClientResilienceExtensions.IdempotencyKeyHeader, "c1_b1_WIN_1");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Unsafe_request_without_a_key_is_not_retried()
    {
        var (client, handler) = Build();

        using var response = await client.PostAsync(new Uri("http://wallet/credits"), null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        handler.Calls.ShouldBe(1);
    }

    private static (HttpClient Client, FailOnceHandler Handler) Build()
    {
        var handler = new FailOnceHandler();
        var services = new ServiceCollection();
        services.AddHttpClient("wallet")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddKeyedResilience();
        var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("wallet");
        return (client, handler);
    }

    private sealed class FailOnceHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        }
    }
}
