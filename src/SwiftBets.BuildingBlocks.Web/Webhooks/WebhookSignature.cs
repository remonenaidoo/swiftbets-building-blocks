using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SwiftBets.BuildingBlocks.Web.Webhooks;

public enum WebhookHash
{
    Sha256,
    Sha512,
}

public enum WebhookVerdict
{
    Valid,
    Missing,
    Malformed,
    Mismatch,
    Expired,
}

/// <summary>
/// HMAC signatures over a webhook's raw body, as lower-case hex. Several secrets may be current at once so a provider
/// secret can be rotated without dropping deliveries; any of them verifies. Comparison is constant-time.
/// </summary>
public sealed class WebhookSignature
{
    private readonly byte[][] _secrets;
    private readonly WebhookHash _hash;

    public WebhookSignature(IReadOnlyList<string> secrets, WebhookHash hash = WebhookHash.Sha256)
    {
        if (secrets.Count == 0 || secrets.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("At least one non-empty webhook secret is required.", nameof(secrets));
        }

        _secrets = [.. secrets.Select(Encoding.UTF8.GetBytes)];
        _hash = hash;
    }

    /// <summary>Signs with the first (newest) secret.</summary>
    public string Sign(ReadOnlySpan<byte> payload) => Convert.ToHexStringLower(Mac(_secrets[0], payload));

    /// <summary>Checks a bare hex signature of the body, the scheme of providers that do not timestamp (e.g. HMAC-SHA512 of the body).</summary>
    public WebhookVerdict Verify(ReadOnlySpan<byte> body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return WebhookVerdict.Missing;
        }

        byte[] presented;
        try
        {
            presented = Convert.FromHexString(signature.Trim());
        }
        catch (FormatException)
        {
            return WebhookVerdict.Malformed;
        }

        foreach (var secret in _secrets)
        {
            if (CryptographicOperations.FixedTimeEquals(Mac(secret, body), presented))
            {
                return WebhookVerdict.Valid;
            }
        }

        return WebhookVerdict.Mismatch;
    }

    /// <summary>The header value for the timestamped scheme: <c>t=&lt;unix seconds&gt;,v1=&lt;hex hmac of "t.body"&gt;</c>.</summary>
    public string SignTimestamped(ReadOnlySpan<byte> body, DateTimeOffset at)
    {
        var timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return $"t={timestamp},v1={Sign(Timestamped(timestamp, body))}";
    }

    /// <summary>Checks the timestamped scheme and refuses a delivery signed further than <paramref name="tolerance"/> from now, so a captured request cannot be replayed later.</summary>
    public WebhookVerdict VerifyTimestamped(ReadOnlySpan<byte> body, string? header, DateTimeOffset now, TimeSpan tolerance)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return WebhookVerdict.Missing;
        }

        string? timestamp = null;
        string? signature = null;
        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("t=", StringComparison.Ordinal))
            {
                timestamp = part[2..];
            }
            else if (part.StartsWith("v1=", StringComparison.Ordinal))
            {
                signature = part[3..];
            }
        }

        if (timestamp is null || signature is null || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return WebhookVerdict.Malformed;
        }

        var verdict = Verify(Timestamped(timestamp, body), signature);
        return verdict == WebhookVerdict.Valid && (now - DateTimeOffset.FromUnixTimeSeconds(seconds)).Duration() > tolerance
            ? WebhookVerdict.Expired
            : verdict;
    }

    private static byte[] Timestamped(string timestamp, ReadOnlySpan<byte> body)
    {
        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var payload = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload.AsSpan(prefix.Length));
        return payload;
    }

    private byte[] Mac(byte[] secret, ReadOnlySpan<byte> payload) => _hash switch
    {
        WebhookHash.Sha512 => HMACSHA512.HashData(secret, payload),
        _ => HMACSHA256.HashData(secret, payload),
    };
}
