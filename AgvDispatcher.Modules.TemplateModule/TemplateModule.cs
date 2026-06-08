using AgvDispatcher.Core.Constants;
using AgvDispatcher.Modules.TemplateModule.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;

namespace AgvDispatcher.Modules.TemplateModule
{
    public class TemplateModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public TemplateModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            // _regionManager.RegisterViewWithRegion(RegionNames.MainRegion, typeof(TemplateView));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            
        }
    }
}
