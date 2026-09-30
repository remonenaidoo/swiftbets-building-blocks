using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Core;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class ConfigurableFaultPointTests
{
    [Fact]
    public async Task Armed_point_throws_exactly_the_armed_number_of_times()
    {
        var faults = Create(enabled: true, Environments.Development);
        faults.Arm("placement.after-reserve");

        await Should.ThrowAsync<FaultInjectedException>(() => faults.HitAsync("placement.after-reserve", TestContext.Current.CancellationToken).AsTask());
        await faults.HitAsync("placement.after-reserve", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Disabled_fault_injection_never_throws_and_refuses_to_arm()
    {
        var faults = Create(enabled: false, Environments.Development);

        await faults.HitAsync("placement.after-reserve", TestContext.Current.CancellationToken);
        Should.Throw<InvalidOperationException>(() => faults.Arm("placement.after-reserve"));
    }

    [Fact]
    public void Enabling_fault_injection_in_production_fails_fast() =>
        Should.Throw<InvalidOperationException>(() => Create(enabled: true, Environments.Production));

    private static ConfigurableFaultPoint Create(bool enabled, string environment) =>
        new(Options.Create(new FaultInjectionOptions { Enabled = enabled }), new HostingEnvironment { EnvironmentName = environment });
}
