using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;

namespace SwiftBets.BuildingBlocks.Observability;

public static class ObservabilityRegistration
{
    /// <summary>Serilog JSON to stdout, OpenTelemetry tracing (OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set), health checks and graceful shutdown.</summary>
    public static WebApplicationBuilder AddSwiftBetsObservability(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(new RenderedCompactJsonFormatter()));

        var tracing = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(trace => trace
                .AddSource("SwiftBets.*")
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !IsOperational(context.Request.Path))
                .AddHttpClientInstrumentation());
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            tracing.WithTracing(trace => trace.AddOtlpExporter());
        }

        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), [HealthTags.Live]);
        builder.Services.AddTransient<CorrelationPropagationHandler>();
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
        return builder;
    }

    public static WebApplication UseSwiftBetsObservability(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
            exception is not null || context.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
            : IsOperational(context.Request.Path) ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information);
        app.UseHttpMetrics();
        return app;
    }

    public static IEndpointRouteBuilder MapSwiftBetsOperationalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = c => c.Tags.Contains(HealthTags.Live), ResponseWriter = WriteHealthAsync });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(HealthTags.Ready), ResponseWriter = WriteHealthAsync });
        endpoints.MapMetrics("/metrics");
        return endpoints;
    }

    public static IHttpClientBuilder AddCorrelationPropagation(this IHttpClientBuilder builder) =>
        builder.AddHttpMessageHandler<CorrelationPropagationHandler>();

    private static bool IsOperational(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.Ordinal) || path.StartsWithSegments("/metrics", StringComparison.Ordinal);

    private static Task WriteHealthAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), description = e.Value.Description }),
        });
    }
}
