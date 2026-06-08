using AgvDispatcher.Modules.DataQueryModule.Services;
using AgvDispatcher.Modules.DataQueryModule.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace AgvDispatcher.Modules.DataQueryModule
{
    public class DataQueryModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            containerProvider.Resolve<LowBatteryEventRecordStore>();
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<LowBatteryEventRecordStore>();
            containerRegistry.RegisterForNavigation<DataQueryWorkspaceView>();
        }
    }
}
