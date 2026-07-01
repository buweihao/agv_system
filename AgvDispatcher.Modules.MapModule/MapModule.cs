using AgvDispatcher.Core.Constants;
using AgvDispatcher.Core.Contracts.MapManagement.Interfaces;
using AgvDispatcher.Modules.MapModule.Services;
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
            containerRegistry.RegisterSingleton<MockMapStore>();
            containerRegistry.RegisterSingleton<MapStaticValidator>();
            containerRegistry.RegisterSingleton<MockMapService>();
            if (!containerRegistry.IsRegistered<IMapManagementService>())
            {
                containerRegistry.RegisterSingleton<IMapManagementService, MockMapManagementService>();
            }
        }
    }
}
