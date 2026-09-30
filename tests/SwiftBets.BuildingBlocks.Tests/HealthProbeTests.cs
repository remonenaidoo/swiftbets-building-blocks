using SwiftBets.BuildingBlocks.Observability;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class HealthProbeTests
{
    [Fact]
    public void Normal_start_is_not_a_probe() =>
        HealthProbe.TryRun(["--urls", "http://+:8080"]).ShouldBeNull();

    [Fact]
    public void Probe_fails_when_nothing_is_listening()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_HTTP_PORTS", "1");
        try
        {
            HealthProbe.TryRun([HealthProbe.Argument]).ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_HTTP_PORTS", null);
        }
    }
}
