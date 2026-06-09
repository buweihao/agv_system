using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockChargeService : IChargeService
    {
        private readonly IVehicleService _vehicleService;
        private readonly ITaskService _taskService;
        private readonly List<ChargeStation> _stations = MockData.CreateChargeStations().ToList();

        public MockChargeService(IVehicleService vehicleService, ITaskService taskService)
        {
            _vehicleService = vehicleService;
            _taskService = taskService;
        }

        public IReadOnlyList<ChargeStation> GetStations()
        {
            return _stations.ToArray();
        }

        public ChargeStation? GetStation(string stationId)
        {
            return _stations.FirstOrDefault(station =>
                string.Equals(station.StationId, stationId, StringComparison.OrdinalIgnoreCase));
        }

        public bool ShouldCharge(string vehicleId)
        {
            return (_vehicleService.GetVehicleStatus(vehicleId)?.BatteryLevel ?? 100) <= 20;
        }

        public ChargeRecommendation RecommendStation(string vehicleId)
        {
            var status = _vehicleService.GetVehicleStatus(vehicleId);
            var station = _stations
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
                Reason = station is null ? "暂无可用充电桩" : "选择空闲且排队权重最低的充电桩"
            };
        }

        public TaskOrder CreateChargeTask(string vehicleId, string stationId)
        {
            var station = GetStation(stationId);
            return _taskService.CreateTask(new TaskCreateRequest
            {
                TaskType = "充电",
                SourceNodeId = _vehicleService.GetVehicleStatus(vehicleId)?.LocationText ?? string.Empty,
                TargetNodeId = station?.NodeId ?? stationId,
                Priority = TaskPriority.High,
                CreatedBy = "MockChargeService",
                Attributes = new Dictionary<string, string>
                {
                    ["VehicleId"] = vehicleId,
                    ["StationId"] = stationId
                }
            });
        }
    }
}
