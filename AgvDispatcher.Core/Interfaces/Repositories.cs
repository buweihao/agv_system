using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IVehicleRepository
    {
        Task<IReadOnlyList<Vehicle>> GetAllAsync();

        Task<Vehicle?> GetByIdAsync(string vehicleId);

        Task SaveAsync(Vehicle vehicle);
        
        Task DeleteAsync(string vehicleId);
    }

    public interface IChargeStationRepository
    {
        Task<IReadOnlyList<ChargeStation>> GetAllAsync();

        Task<ChargeStation?> GetByIdAsync(string stationId);

        Task SaveAsync(ChargeStation station);

        Task DeleteAsync(string stationId);

        Task<IReadOnlyList<ChargeSessionRecord>> GetSessionsAsync(string? vehicleId = null, string? stationId = null);

        Task AddSessionAsync(ChargeSessionRecord session);

        Task UpdateSessionAsync(ChargeSessionRecord session);
    }

    public interface IMapRepository
    {
        Task<IReadOnlyList<MapNode>> GetNodesAsync();

        Task<IReadOnlyList<MapNode>> GetNodesAsync(string mapId, string mapVersion);

        Task<IReadOnlyList<MapEdge>> GetEdgesAsync();

        Task<IReadOnlyList<MapEdge>> GetEdgesAsync(string mapId, string mapVersion);

        Task SaveNodeAsync(MapNode node);

        Task SaveEdgeAsync(MapEdge edge);

        Task DeleteNodeAsync(string nodeId);

        Task DeleteEdgeAsync(string edgeId);
    }

    public interface IMapVersionRepository
    {
        Task<MapVersionEntity?> GetActiveAsync();

        Task<MapVersionEntity?> GetAsync(string mapId, string mapVersion);

        Task<IReadOnlyList<MapVersionEntity>> GetAllAsync(string? mapId = null);

        Task SaveAsync(MapVersionEntity version);

        Task SetActiveAsync(string mapId, string mapVersion);

        Task DeleteAsync(string mapId, string mapVersion);
    }

    public interface IMapLocationAliasRepository
    {
        Task<IReadOnlyList<MapLocationAlias>> GetAllAsync();

        Task<IReadOnlyList<MapLocationAlias>> GetAllAsync(string mapId, string mapVersion);

        Task SaveAsync(MapLocationAlias alias);

        Task DeleteAsync(string aliasId);
    }

    public interface IMapAreaRepository
    {
        Task<IReadOnlyList<MapArea>> GetAllAsync(string mapId, string mapVersion);

        Task SaveAsync(MapArea area);

        Task DeleteAsync(string mapId, string mapVersion, string areaId);
    }

    public interface ITaskTemplateRepository
    {
        Task<IReadOnlyList<TaskTemplateConfig>> GetAllAsync();

        Task SaveAsync(TaskTemplateConfig template);
    }

    public interface ISystemParameterRepository
    {
        Task<IReadOnlyList<ParameterConfig>> GetAllAsync();

        Task SaveAsync(ParameterConfig parameter);
    }

    public interface IAlarmRepository
    {
        Task<IReadOnlyList<AlarmEvent>> GetAllAsync();

        Task<IReadOnlyList<AlarmEvent>> GetActiveAsync();

        Task<IReadOnlyList<AlarmEvent>> QueryAsync(AlarmQuery query);

        Task<AlarmEvent?> GetByIdAsync(string alarmId);

        Task SaveAsync(AlarmEvent alarm);
    }

    public interface IOperationLogRepository
    {
        Task AddAsync(OperationLog log);

        Task<IReadOnlyList<OperationLog>> QueryAsync(OperationLogQuery query);
    }
}
