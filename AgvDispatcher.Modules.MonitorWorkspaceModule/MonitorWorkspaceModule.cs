using Prism.Ioc;
using Prism.Modularity;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Modules.MonitorWorkspaceModule.Services;
using AgvDispatcher.Modules.MonitorWorkspaceModule.Views;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule
{
    public class MonitorWorkspaceModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IVehicleStatusPublisher, VehicleStatusPublisher>();
            containerRegistry.RegisterForNavigation<MonitorLayoutView>();
        }
    }
}
