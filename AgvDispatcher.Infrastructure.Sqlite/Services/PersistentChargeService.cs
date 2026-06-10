using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentChargeService : IChargeService
    {
        private readonly IChargeStationRepository _stations;
        private readonly IVehicleService _vehicleService;
        private readonly ITaskService _taskService;

        public PersistentChargeService(IChargeStationRepository stations, IVehicleService vehicleService, ITaskService taskService)
        {
            _stations = stations;
            _vehicleService = vehicleService;
            _taskService = taskService;
        }

        public IReadOnlyList<ChargeStation> GetStations()
        {
            return _stations.GetAllAsync().GetAwaiter().GetResult();
        }

        public ChargeStation? GetStation(string stationId)
        {
            return _stations.GetByIdAsync(stationId).GetAwaiter().GetResult();
        }

        public bool ShouldCharge(string vehicleId)
        {
            return (_vehicleService.GetVehicleStatus(vehicleId)?.BatteryLevel ?? 100) <= 20;
        }

        public ChargeRecommendation RecommendStation(string vehicleId)
        {
            var status = _vehicleService.GetVehicleStatus(vehicleId);
            var station = GetStations()
                .Where(item => item.State == ChargeStationState.Available && item.IsEnabled)
                .OrderBy(item => item.QueueWeight)
                .FirstOrDefault();

            return new ChargeRecommendation
            {
                VehicleId = vehicleId,
                StationId = station?.StationId,
                StationName = station?.Name,
                HasAvailableStation = station is not null,
                BatteryLevel = status?.BatteryLevel ?? 0,
                EstimatedDistance = station is null ? 0 : 120 + station.QueueWeight * 30,
                QueueLength = station is null ? 0 : (int)Math.Round(station.QueueWeight),
                Reason = station is null ? "No available charge station." : "Selected the available station with the lowest queue weight."
            };
        }

        public TaskOrder CreateChargeTask(string vehicleId, string stationId)
        {
            var station = GetStation(stationId);
            return _taskService.CreateTask(new TaskCreateRequest
            {
                TaskType = "Charge",
                SourceNodeId = _vehicleService.GetVehicleStatus(vehicleId)?.LocationText ?? string.Empty,
                TargetNodeId = station?.NodeId ?? stationId,
                Priority = TaskPriority.High,
                CreatedBy = nameof(PersistentChargeService),
                Attributes = new Dictionary<string, string>
                {
                    ["VehicleId"] = vehicleId,
                    ["StationId"] = stationId
                }
            });
        }
    }
}
