using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SwiftBets.BuildingBlocks.Core;

public static class OptionsRegistration
{
    /// <summary>Binds a section and fails host startup when its data annotations are violated.</summary>
    public static IServiceCollection AddValidatedOptions<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }

    public static IServiceCollection AddFaultInjection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<FaultInjectionOptions>(configuration, FaultInjectionOptions.SectionName);
        services.AddSingleton<ConfigurableFaultPoint>();
        services.AddSingleton<IFaultPoint>(sp => sp.GetRequiredService<ConfigurableFaultPoint>());
        return services;
    }
}
