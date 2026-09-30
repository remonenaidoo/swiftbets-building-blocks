using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SwiftBets.BuildingBlocks.Web;

/// <summary>Unexpected exceptions become a 500 envelope that carries the correlation id and never the exception text.</summary>
public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is BadHttpRequestException badRequest)
        {
            await ErrorEnvelopes.WriteAsync(httpContext, ErrorEnvelopes.Create(httpContext, badRequest.StatusCode, "bad_request")).ConfigureAwait(false);
            return true;
        }

        LogUnhandled(exception, httpContext.Request.Method, httpContext.Request.Path);
        await ErrorEnvelopes.WriteAsync(httpContext, ErrorEnvelopes.Create(httpContext, StatusCodes.Status500InternalServerError, "internal_error")).ConfigureAwait(false);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path}")]
    private partial void LogUnhandled(Exception exception, string method, string path);
}
