using AgvDispatcher.Modules.DataQueryModule.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace AgvDispatcher.Modules.DataQueryModule
{
    public class DataQueryModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<DataQueryWorkspaceView>();
        }
    }
}
