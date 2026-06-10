using Prism.Ioc;
using Prism.Modularity;
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
            containerRegistry.RegisterForNavigation<MonitorLayoutView>();
        }
    }
}
