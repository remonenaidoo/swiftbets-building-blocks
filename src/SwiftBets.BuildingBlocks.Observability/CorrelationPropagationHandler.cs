using SwiftBets.BuildingBlocks.Core;

namespace SwiftBets.BuildingBlocks.Observability;

public sealed class CorrelationPropagationHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (CorrelationContext.CorrelationId is { } correlationId && !request.Headers.Contains(CorrelationContext.HeaderName))
        {
            request.Headers.Add(CorrelationContext.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
