using Microsoft.Extensions.DependencyInjection;
using ShowTrigger.Modules;

namespace ShowTrigger;

internal static class ModuleDependencyInjection
{
    internal static IServiceCollection AddModules(this IServiceCollection services)
    {
        services.AddSingleton<ShowTriggerModule>();
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<ShowTriggerModule>());
        return services;
    }
}
