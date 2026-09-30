using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.BuildingBlocks.Web;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class FaultEndpointTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["FaultInjection:Enabled"] = "true", ["Jwt:Authority"] = TestJwt.Issuer });
        builder.Services.AddSwiftBetsWeb();
        builder.Services.AddFaultInjection(builder.Configuration);
        builder.Services.AddSwiftBetsJwtBearer(builder.Configuration);
        builder.Services.UseTestJwt();
        _app = builder.Build();
        _app.UseSwiftBetsWeb();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapSwiftBetsFaultEndpoints();
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Operator_arms_a_fault_that_the_next_hit_raises()
    {
        using var response = await SendAsync("Operator");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await Should.ThrowAsync<FaultInjectedException>(() =>
            _app.Services.GetRequiredService<IFaultPoint>().HitAsync("settlement.settler.drop", TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task Punter_cannot_arm_faults()
    {
        using var response = await SendAsync("Punter");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private Task<HttpResponseMessage> SendAsync(string role)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/faults/settlement.settler.drop?times=1");
        request.Headers.Authorization = new("Bearer", TestJwt.Issue("someone", role));
        return _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
