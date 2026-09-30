using Microsoft.AspNetCore.Http;

namespace SwiftBets.BuildingBlocks.Web;

/// <summary>Baseline headers for JSON APIs; the dashboard's nginx sets its own nonce-based CSP.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            headers.CacheControl = headers.CacheControl.Count == 0 ? "no-store" : headers.CacheControl;
            return Task.CompletedTask;
        });
        return next(context);
    }
}
