using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Services;
using Prism.Ioc;

namespace AgvDispatcher.Infrastructure.Mock
{
    public static class MockServiceRegistration
    {
        public static void RegisterMockServices(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IPathPlanningService, DijkstraPathPlanningService>();
            containerRegistry.RegisterSingleton<IVehicleStateStore, MockVehicleStateStore>();
            containerRegistry.RegisterSingleton<IVehicleService, MockVehicleService>();
            containerRegistry.RegisterSingleton<ITaskService, MockTaskService>();
            containerRegistry.RegisterSingleton<IChargeService, MockChargeService>();
            containerRegistry.RegisterSingleton<IAlarmService, MockAlarmService>();
            containerRegistry.RegisterSingleton<IMapService, MockMapService>();
            containerRegistry.RegisterSingleton<ISignalService, MockSignalService>();
            containerRegistry.RegisterSingleton<IDataQueryService, MockDataQueryService>();
            containerRegistry.RegisterSingleton<ITaskConfigService, MockTaskConfigService>();
        }
    }
}
