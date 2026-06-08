using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using AgvDispatcher.Core.Constants;
using AgvDispatcher.Modules.MonitorWorkspaceModule.Views;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule
{
    public class MonitorWorkspaceModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public MonitorWorkspaceModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<MonitorLayoutView>();
        }
    }
}
