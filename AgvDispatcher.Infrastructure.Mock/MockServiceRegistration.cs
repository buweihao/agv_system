using AgvDispatcher.Core.Contracts.Planning.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Interfaces;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Services;
using AgvDispatcher.Infrastructure.Mock.Planning;
using AgvDispatcher.Infrastructure.Mock.Reservations;
using AgvDispatcher.Infrastructure.Mock.Traffic;
using Prism.Ioc;

namespace AgvDispatcher.Infrastructure.Mock
{
    public static class MockServiceRegistration
    {
        public static void RegisterMockServices(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IPathPlanningService, DijkstraPathPlanningService>();
            containerRegistry.RegisterSingleton<IPathPlanner, DijkstraPathPlanner>();
            containerRegistry.RegisterSingleton<IVehicleStateStore, MockVehicleStateStore>();
            containerRegistry.RegisterSingleton<IVehicleService, MockVehicleService>();
            containerRegistry.RegisterSingleton<ITaskService, MockTaskService>();
            containerRegistry.RegisterSingleton<IChargeService, MockChargeService>();
            containerRegistry.RegisterSingleton<IAlarmService, MockAlarmService>();
            // containerRegistry.RegisterSingleton<IMapService, MockMapService>();
            containerRegistry.RegisterSingleton<ISignalService, MockSignalService>();
            containerRegistry.RegisterSingleton<IDataQueryService, MockDataQueryService>();
            containerRegistry.RegisterSingleton<ITaskConfigService, MockTaskConfigService>();
            containerRegistry.RegisterSingleton<ITrafficControlService, MockTrafficControlService>();
            containerRegistry.RegisterSingleton<IRouteReservationService, MockRouteReservationService>();
        }
    }
}
