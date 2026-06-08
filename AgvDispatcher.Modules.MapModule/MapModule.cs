using AgvDispatcher.Core.Constants;
using AgvDispatcher.Modules.MapModule.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;

namespace AgvDispatcher.Modules.MapModule
{
    public class MapModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public MapModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            _regionManager.RegisterViewWithRegion(RegionNames.MapRegion, typeof(MapView));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            
        }
    }
}
