using AgvDispatcher.Modules.SystemConfigModule.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace AgvDispatcher.Modules.SystemConfigModule
{
    public class SystemConfigModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<SystemSettingView>();
        }
    }
}
