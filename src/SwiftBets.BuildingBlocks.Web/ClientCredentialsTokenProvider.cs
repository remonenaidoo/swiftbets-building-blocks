using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace SwiftBets.BuildingBlocks.Web;

/// <summary>Fetches a Service-role token with client credentials and reuses it until shortly before it expires.</summary>
public sealed class ClientCredentialsTokenProvider(HttpClient http, IOptions<ClientCredentialsOptions> options, TimeProvider time) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _renewAt;

    public async ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is { } cached && time.GetUtcNow() < _renewAt)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is { } fresh && time.GetUtcNow() < _renewAt)
            {
                return fresh;
            }

            using var response = await http.PostAsJsonAsync(
                new Uri(options.Value.TokenEndpoint),
                new { grantType = "client_credentials", clientId = options.Value.ClientId, clientSecret = options.Value.ClientSecret },
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<TokenBody>(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Empty token response.");
            _token = body.AccessToken;
            _renewAt = time.GetUtcNow().AddSeconds(Math.Max(30, body.ExpiresIn / 2));
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private sealed record TokenBody(string AccessToken, int ExpiresIn);
}
