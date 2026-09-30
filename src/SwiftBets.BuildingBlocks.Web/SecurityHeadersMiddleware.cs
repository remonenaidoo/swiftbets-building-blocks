using Microsoft.AspNetCore.Http;

namespace SwiftBets.BuildingBlocks.Web;

/// <summary>Baseline headers for JSON APIs. Headers a proxied upstream already set (the dashboard's nonce CSP) are kept.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            headers.TryAdd("Cross-Origin-Resource-Policy", "same-origin");
            headers.TryAdd("Cache-Control", "no-store");
            return Task.CompletedTask;
        });
        return next(context);
    }
}
