using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Web;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class ClientCredentialsTokenProviderTests
{
    [Fact]
    public async Task Rejected_token_is_replaced_on_the_next_call()
    {
        using var provider = Create();
        var first = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        provider.Invalidate(first);

        (await provider.GetTokenAsync(TestContext.Current.CancellationToken)).ShouldBe("token-2");
    }

    [Fact]
    public async Task Rejecting_some_other_token_keeps_the_current_one()
    {
        using var provider = Create();
        var first = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        provider.Invalidate("an-older-token");

        (await provider.GetTokenAsync(TestContext.Current.CancellationToken)).ShouldBe(first);
    }

    private static ClientCredentialsTokenProvider Create() =>
        new(new HttpClient(new IssuingHandler()), Options.Create(new ClientCredentialsOptions { TokenEndpoint = "http://identity.test/auth/token", ClientId = "payout", ClientSecret = "s" }), TimeProvider.System);

    private sealed class IssuingHandler : HttpMessageHandler
    {
        private int _issued;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { accessToken = $"token-{Interlocked.Increment(ref _issued)}", expiresIn = 600 }) });
    }
}
