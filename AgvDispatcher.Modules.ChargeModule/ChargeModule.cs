using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using AgvDispatcher.Core.Constants;
using AgvDispatcher.Modules.ChargeModule.Services;
using AgvDispatcher.Modules.ChargeModule.Views;

namespace AgvDispatcher.Modules.ChargeModule
{
    public class ChargeModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public ChargeModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<ChargeStationRecommendationService>();
            containerRegistry.RegisterForNavigation<ChargeWorkspaceView>();
        }
    }
}
