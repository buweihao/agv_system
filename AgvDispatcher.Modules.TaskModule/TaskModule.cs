using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using AgvDispatcher.Core.Constants;
using AgvDispatcher.Modules.TaskModule.ViewModels;
using AgvDispatcher.Modules.TaskModule.Views;

namespace AgvDispatcher.Modules.TaskModule
{
    public class TaskModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public TaskModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<TaskMainPanelViewModel>();
            containerRegistry.RegisterSingleton<TaskLeftPanelViewModel>();
            containerRegistry.RegisterForNavigation<TaskWorkspaceView>();
        }
    }
}
