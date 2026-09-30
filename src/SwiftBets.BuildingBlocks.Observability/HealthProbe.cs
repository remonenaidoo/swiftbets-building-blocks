using System.Globalization;

namespace SwiftBets.BuildingBlocks.Observability;

/// <summary>
/// Chiseled images have no shell or curl, so a container health check runs the service binary itself with
/// <c>--healthcheck</c>, which probes the local readiness endpoint and exits 0 or 1.
/// </summary>
public static class HealthProbe
{
    public const string Argument = "--healthcheck";

    public static int? TryRun(string[] args)
    {
        if (!args.Contains(Argument, StringComparer.Ordinal))
        {
            return null;
        }

        var port = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080").Split(';', ',')[0];
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = client.GetAsync(new Uri($"http://127.0.0.1:{int.Parse(port, CultureInfo.InvariantCulture)}/health/ready")).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            return 1;
        }
        catch (TaskCanceledException)
        {
            return 1;
        }
    }
}
