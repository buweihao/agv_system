using AgvDispatcher.Modules.SignalModule.Services;
using AgvDispatcher.Modules.SignalModule.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace AgvDispatcher.Modules.SignalModule
{
    public class SignalModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            containerProvider.Resolve<SignalLowBatteryLogStore>();
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<SignalLowBatteryLogStore>();
            containerRegistry.RegisterForNavigation<SignalWorkspaceView>();
        }
    }
}
