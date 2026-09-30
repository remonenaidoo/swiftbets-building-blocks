using System.Net;
using System.Net.Http.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class WebPipelineTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.AddSwiftBetsObservability("test");
        builder.Services.AddSwiftBetsWeb();
        builder.Services.AddScoped<IValidator<StakeRequest>, StakeRequestValidator>();
        _app = builder.Build();
        _app.UseSwiftBetsObservability();
        _app.UseSwiftBetsWeb();
        _app.MapSwiftBetsOperationalEndpoints();
        _app.MapGet("/boom", string () => throw new InvalidOperationException("secret connection string"));
        _app.MapGet("/rule", (HttpContext context) => Result.Failure<int>(Error.BusinessRule("stake_too_high", "Stake exceeds the limit.")).ToHttpResult(context));
        _app.MapPost("/stakes", (StakeRequest request) => Results.Ok(request)).AddEndpointFilter<ValidationFilter<StakeRequest>>();
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Unexpected_exception_becomes_an_envelope_without_internals()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/boom");
        request.Headers.Add(CorrelationContext.HeaderName, "corr-42");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        body.ShouldContain("\"code\":\"internal_error\"");
        body.ShouldContain("\"correlationId\":\"corr-42\"");
        body.ShouldNotContain("secret");
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
    }

    [Fact]
    public async Task Business_rule_failure_maps_to_422_with_its_code()
    {
        var envelope = await GetEnvelopeAsync(await _client.GetAsync(new Uri("/rule", UriKind.Relative), TestContext.Current.CancellationToken));

        envelope.Status.ShouldBe(422);
        envelope.Code.ShouldBe("stake_too_high");
    }

    [Fact]
    public async Task Invalid_request_is_rejected_with_field_errors()
    {
        var envelope = await GetEnvelopeAsync(await _client.PostAsJsonAsync("/stakes", new StakeRequest(-5), TestContext.Current.CancellationToken));

        envelope.Status.ShouldBe(400);
        envelope.Errors!.ShouldContain(e => e.Field == "minorUnits");
    }

    [Fact]
    public async Task Liveness_endpoint_reports_healthy()
    {
        using var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<ErrorEnvelope> GetEnvelopeAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.Content.Headers.ContentType!.MediaType.ShouldBe(ErrorEnvelope.MediaType);
            return (await response.Content.ReadFromJsonAsync<ErrorEnvelope>(ContractJson.Options, TestContext.Current.CancellationToken))!;
        }
    }

    public sealed record StakeRequest(long MinorUnits);

    private sealed class StakeRequestValidator : AbstractValidator<StakeRequest>
    {
        public StakeRequestValidator() => RuleFor(r => r.MinorUnits).GreaterThan(0);
    }
}
