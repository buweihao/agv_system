using AgvDispatcher.Modules.TaskConfigModule.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace AgvDispatcher.Modules.TaskConfigModule
{
    public class TaskConfigModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<TaskConfigWorkspaceView>();
        }
    }
}
