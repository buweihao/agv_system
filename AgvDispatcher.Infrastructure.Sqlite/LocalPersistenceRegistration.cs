using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Infrastructure.Mock;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using AgvDispatcher.Infrastructure.Sqlite.Repositories;
using AgvDispatcher.Infrastructure.Sqlite.Services;
using AgvDispatcher.Core.Services;
using Microsoft.EntityFrameworkCore;
using Prism.Ioc;
using System.Net.Http;
using AgvDispatcher.Infrastructure.Okapi;

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

            containerRegistry.RegisterSingleton<ITaskService, PersistentTaskService>();
            containerRegistry.RegisterSingleton<ISignalService, MockSignalService>();

            containerRegistry.RegisterSingleton<IVehicleRepository, VehicleRepository>();
            containerRegistry.RegisterSingleton<IChargeStationRepository, ChargeStationRepository>();
            containerRegistry.RegisterSingleton<IMapRepository, MapRepository>();
            containerRegistry.RegisterSingleton<IMapLocationAliasRepository, MapLocationAliasRepository>();
            containerRegistry.RegisterSingleton<IMapValidationService, PersistentMapValidationService>();
            containerRegistry.RegisterSingleton<ITaskTemplateRepository, TaskTemplateRepository>();
            containerRegistry.RegisterSingleton<ISystemParameterRepository, SystemParameterRepository>();
            containerRegistry.RegisterSingleton<IAlarmRepository, AlarmRepository>();
            containerRegistry.RegisterSingleton<IOperationLogRepository, OperationLogRepository>();

            containerRegistry.RegisterSingleton<IVehicleStateStore, PersistentVehicleStateStore>();
            containerRegistry.RegisterSingleton<IVehicleStatusPublisher, VehicleStatusPublisher>();

            // Okapi Registration
            containerRegistry.RegisterInstance(new HttpClient());
            containerRegistry.RegisterInstance(new OkapiOptions());
            containerRegistry.RegisterSingleton<OkapiProtocolLogger>();
            containerRegistry.RegisterSingleton<OkapiClient>();
            containerRegistry.RegisterSingleton<OkapiCallbackServer>();
            containerRegistry.RegisterSingleton<OkapiPointMapper>();
            containerRegistry.RegisterSingleton<OkapiVehicleIdentityMapper>();
            containerRegistry.RegisterSingleton<OkapiDtoMapper>();
            containerRegistry.RegisterSingleton<OkapiStatusSyncService>();
            containerRegistry.RegisterSingleton<OkapiTaskStateHandler>();
            containerRegistry.RegisterSingleton<OkapiAreaControlHandler>();

            containerRegistry.RegisterSingleton<ProfileBasedMockVehicleAdapterFactory>();
            containerRegistry.RegisterSingleton<OkapiVehicleAdapterFactory>();
            containerRegistry.RegisterSingleton<IVehicleAdapterFactory, CompositeVehicleAdapterFactory>();
            containerRegistry.RegisterSingleton<IVehicleAdapterManager, VehicleAdapterManager>();
            containerRegistry.RegisterSingleton<ITaskExecutionSimulator, MockTaskExecutionSimulator>();
            containerRegistry.RegisterSingleton<IDispatchScoringService, DispatchScoringService>();
            containerRegistry.RegisterSingleton<IDispatchService, AdapterDispatchService>();

            containerRegistry.RegisterSingleton<IVehicleService, PersistentVehicleService>();
            containerRegistry.RegisterSingleton<IChargeService, PersistentChargeService>();
            containerRegistry.RegisterSingleton<ITaskRecoveryService, PersistentTaskRecoveryService>();
            containerRegistry.RegisterSingleton<IAlarmService, PersistentAlarmService>();
            containerRegistry.RegisterSingleton<IMapService, PersistentMapService>();
            containerRegistry.RegisterSingleton<ITaskConfigService, PersistentTaskConfigService>();
            containerRegistry.RegisterSingleton<IOperationLogService, PersistentOperationLogService>();
            containerRegistry.RegisterSingleton<IAuditTrailService, PersistentAuditTrailService>();
            containerRegistry.RegisterSingleton<IDataQueryService, PersistentDataQueryService>();
        }

        private static string GetDatabasePath()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(directory, "agv_dispatcher.db");
        }
    }
}
