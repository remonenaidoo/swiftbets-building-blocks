using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.BuildingBlocks.Web;

public static class ErrorEnvelopes
{
    public static ErrorEnvelope Create(HttpContext context, int status, string code, string? detail = null, IReadOnlyList<FieldError>? errors = null) =>
        new(
            Type: $"https://swiftbets.dev/errors/{code}",
            Title: ReasonPhrases.GetReasonPhrase(status),
            Status: status,
            Code: code,
            CorrelationId: CorrelationContext.CorrelationId ?? context.TraceIdentifier,
            Detail: detail,
            Instance: context.Request.Path,
            Errors: errors);

    public static IResult ToResult(ErrorEnvelope envelope) =>
        Results.Json(envelope, ContractJson.Options, ErrorEnvelope.MediaType, envelope.Status);

    public static Task WriteAsync(HttpContext context, ErrorEnvelope envelope)
    {
        context.Response.StatusCode = envelope.Status;
        return context.Response.WriteAsJsonAsync(envelope, ContractJson.Options, ErrorEnvelope.MediaType);
    }
}
