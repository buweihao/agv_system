using AgvDispatcher.Application.Dispatching;
using AgvDispatcher.Core.Contracts.Dispatching.Interfaces;
using AgvDispatcher.Core.Interfaces;
using Prism.Ioc;

namespace AgvDispatcher.Application.DependencyInjection;

public static class ApplicationRegistration
{
    public static void RegisterApplicationServices(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IDispatchScoringService, DispatchScoringService>();
        containerRegistry.RegisterSingleton<IDispatchOrchestrationService, DispatchOrchestrationService>();
        containerRegistry.RegisterSingleton<IDispatchService, AdapterDispatchService>();
    }
}
