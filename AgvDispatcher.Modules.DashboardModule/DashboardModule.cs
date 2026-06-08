using AgvDispatcher.Core.Constants;
using AgvDispatcher.Modules.DashboardModule.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;

namespace AgvDispatcher.Modules.DashboardModule
{
    public class DashboardModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public DashboardModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            _regionManager.RegisterViewWithRegion(RegionNames.LeftPanelRegion, typeof(LeftPanelView));
            _regionManager.RegisterViewWithRegion(RegionNames.RightPanelRegion, typeof(RightPanelView));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            
        }
    }
}
