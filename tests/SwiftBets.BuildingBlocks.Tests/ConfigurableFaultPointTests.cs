using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
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

    [Fact]
    public async Task Point_armed_for_a_duration_fails_every_hit_until_it_expires()
    {
        var time = new FakeTimeProvider();
        var faults = Create(enabled: true, Environments.Development, time);
        faults.ArmFor("wallet.unavailable", TimeSpan.FromSeconds(60));

        for (var i = 0; i < 5; i++)
        {
            await Should.ThrowAsync<FaultInjectedException>(() => faults.HitAsync("wallet.unavailable", TestContext.Current.CancellationToken).AsTask());
        }

        time.Advance(TimeSpan.FromSeconds(61));
        await faults.HitAsync("wallet.unavailable", TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Disabled_fault_injection_refuses_a_timed_outage() =>
        Should.Throw<InvalidOperationException>(() => Create(enabled: false, Environments.Development).ArmFor("wallet.unavailable", TimeSpan.FromSeconds(60)));

    private static ConfigurableFaultPoint Create(bool enabled, string environment, TimeProvider? time = null) =>
        new(Options.Create(new FaultInjectionOptions { Enabled = enabled }), new HostingEnvironment { EnvironmentName = environment }, time);
}
