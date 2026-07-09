using AgvDispatcher.DebugDashboard.Services;
using AgvDispatcher.DebugDashboard.ViewModels;
using AgvDispatcher.DebugDashboard.Views;
using AgvDispatcher.Infrastructure.Mock.Simulation;
using Prism.Ioc;

namespace AgvDispatcher.DebugDashboard;

public static class DebugDashboardRegistration
{
    public static void RegisterDebugDashboard(
        IContainerRegistry containerRegistry,
        DebugDashboardOptions options)
    {
        if (!options.EnableMockSimulationDashboard)
        {
            return;
        }

        containerRegistry.RegisterInstance(options);
        if (!containerRegistry.IsRegistered<IMockFleetSimulationEngine>())
        {
            containerRegistry.RegisterSingleton<IMockFleetSimulationEngine, MockFleetSimulationEngine>();
        }

        containerRegistry.RegisterSingleton<IDebugSnapshotService, DebugSnapshotService>();
        containerRegistry.RegisterSingleton<IMockSimulationService, MockSimulationService>();
        containerRegistry.RegisterSingleton<IMockScenarioController, MockScenarioController>();
        containerRegistry.RegisterSingleton<IMockVehicleWindowService, MockVehicleWindowService>();
        containerRegistry.Register<DebugDashboardViewModel>();
        containerRegistry.Register<DebugDashboardWindow>();
    }
}
