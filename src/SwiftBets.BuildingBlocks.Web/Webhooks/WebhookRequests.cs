using Microsoft.AspNetCore.Http;

namespace SwiftBets.BuildingBlocks.Web.Webhooks;

public static class WebhookRequests
{
    /// <summary>The exact bytes the provider signed; null when the body is larger than <paramref name="limitBytes"/>.</summary>
    public static async Task<byte[]?> ReadRawBodyAsync(this HttpRequest request, int limitBytes = 256 * 1024)
    {
        if (request.ContentLength > limitBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, request.HttpContext.RequestAborted)) > 0)
        {
            if (buffer.Length + read > limitBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
