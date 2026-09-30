using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SwiftBets.BuildingBlocks.Core;

public sealed class ConfigurableFaultPoint : IFaultPoint
{
    private readonly ConcurrentDictionary<string, int> _armed = new(StringComparer.Ordinal);
    private readonly bool _enabled;

    public ConfigurableFaultPoint(IOptions<FaultInjectionOptions> options, IHostEnvironment environment)
    {
        if (options.Value.Enabled && environment.IsProduction())
        {
            throw new InvalidOperationException("Fault injection cannot be enabled in the Production environment.");
        }

        _enabled = options.Value.Enabled;
    }

    public bool IsEnabled => _enabled;

    /// <summary>Makes the next <paramref name="times"/> hits of <paramref name="name"/> throw.</summary>
    public void Arm(string name, int times = 1)
    {
        if (!_enabled)
        {
            throw new InvalidOperationException("Fault injection is disabled.");
        }

        _armed.AddOrUpdate(name, times, (_, current) => current + times);
    }

    public void Disarm(string name) => _armed.TryRemove(name, out _);

    public IReadOnlyDictionary<string, int> Armed => _armed;

    public ValueTask HitAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            return ValueTask.CompletedTask;
        }

        while (_armed.TryGetValue(name, out var remaining) && remaining > 0)
        {
            if (_armed.TryUpdate(name, remaining - 1, remaining))
            {
                if (remaining == 1)
                {
                    _armed.TryRemove(new KeyValuePair<string, int>(name, 0));
                }

                throw new FaultInjectedException(name);
            }
        }

        return ValueTask.CompletedTask;
    }
}
