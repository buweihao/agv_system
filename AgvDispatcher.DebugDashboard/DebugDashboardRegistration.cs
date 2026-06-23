using AgvDispatcher.DebugDashboard.Services;
using AgvDispatcher.DebugDashboard.ViewModels;
using AgvDispatcher.DebugDashboard.Views;
using Prism.Ioc;

namespace AgvDispatcher.DebugDashboard;

public static class DebugDashboardRegistration
{
    public static void RegisterDebugDashboard(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IDebugSnapshotService, DebugSnapshotService>();
        containerRegistry.Register<DebugDashboardViewModel>();
        containerRegistry.Register<DebugDashboardWindow>();
    }
}
