using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Serilog.Context;
using SwiftBets.BuildingBlocks.Core;

namespace SwiftBets.BuildingBlocks.Observability;

/// <summary>Accepts a well-formed inbound correlation id or mints one, and scopes logs, traces and the response to it.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var inbound = context.Request.Headers[CorrelationContext.HeaderName].ToString();
        var correlationId = CorrelationContext.IsValid(inbound) ? inbound : CorrelationContext.NewId();
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationContext.HeaderName] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (CorrelationContext.Begin(correlationId))
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
