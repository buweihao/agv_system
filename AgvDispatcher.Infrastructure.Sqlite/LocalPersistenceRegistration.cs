using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Infrastructure.Mock;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using AgvDispatcher.Infrastructure.Sqlite.Repositories;
using AgvDispatcher.Infrastructure.Sqlite.Services;
using AgvDispatcher.Core.Services;
using Microsoft.EntityFrameworkCore;
using Prism.Ioc;

namespace AgvDispatcher.Infrastructure.Sqlite
{
    public static class LocalPersistenceRegistration
    {
        public static void RegisterLocalPersistence(IContainerRegistry containerRegistry)
        {
            var databasePath = GetDatabasePath();
            var options = new DbContextOptionsBuilder<AgvDispatcherDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            containerRegistry.RegisterInstance(options);
            containerRegistry.RegisterSingleton<AgvDispatcherDbContext>();
            containerRegistry.RegisterSingleton<LocalPersistenceInitializer>();
            containerRegistry.RegisterSingleton<IPathPlanningService, DijkstraPathPlanningService>();

            containerRegistry.RegisterSingleton<IVehicleStateStore, MockVehicleStateStore>();
            containerRegistry.RegisterSingleton<ITaskService, MockTaskService>();
            containerRegistry.RegisterSingleton<ISignalService, MockSignalService>();
            containerRegistry.RegisterSingleton<IDataQueryService, MockDataQueryService>();

            containerRegistry.RegisterSingleton<IVehicleRepository, VehicleRepository>();
            containerRegistry.RegisterSingleton<IChargeStationRepository, ChargeStationRepository>();
            containerRegistry.RegisterSingleton<IMapRepository, MapRepository>();
            containerRegistry.RegisterSingleton<ITaskTemplateRepository, TaskTemplateRepository>();
            containerRegistry.RegisterSingleton<ISystemParameterRepository, SystemParameterRepository>();
            containerRegistry.RegisterSingleton<IAlarmRepository, AlarmRepository>();
            containerRegistry.RegisterSingleton<IOperationLogRepository, OperationLogRepository>();

            containerRegistry.RegisterSingleton<IVehicleStatusPublisher, VehicleStatusPublisher>();
            containerRegistry.RegisterSingleton<IVehicleAdapterFactory, MockVehicleAdapterFactory>();
            containerRegistry.RegisterSingleton<IVehicleAdapterManager, VehicleAdapterManager>();
            containerRegistry.RegisterSingleton<IDispatchService, AdapterDispatchService>();
            containerRegistry.RegisterSingleton<IVehicleService, PersistentVehicleService>();
            containerRegistry.RegisterSingleton<IChargeService, PersistentChargeService>();
            containerRegistry.RegisterSingleton<IAlarmService, PersistentAlarmService>();
            containerRegistry.RegisterSingleton<IMapService, PersistentMapService>();
            containerRegistry.RegisterSingleton<ITaskConfigService, PersistentTaskConfigService>();
            containerRegistry.RegisterSingleton<IOperationLogService, PersistentOperationLogService>();
        }

        private static string GetDatabasePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var directory = Path.Combine(appData, "AgvDispatcher");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "agv_dispatcher.db");
        }
    }
}
