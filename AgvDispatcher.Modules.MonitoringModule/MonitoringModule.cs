using AgvDispatcher.Core.Constants;
using AgvDispatcher.Modules.MonitoringModule.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;

namespace AgvDispatcher.Modules.MonitoringModule
{
    public class MonitoringModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public MonitoringModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            _regionManager.RegisterViewWithRegion(RegionNames.RobotListRegion, typeof(RobotListView));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            
        }
    }
}
