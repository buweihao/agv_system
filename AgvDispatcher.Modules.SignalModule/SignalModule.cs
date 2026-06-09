using AgvDispatcher.Modules.SignalModule.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace AgvDispatcher.Modules.SignalModule
{
    public class SignalModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<SignalWorkspaceView>();
        }
    }
}
