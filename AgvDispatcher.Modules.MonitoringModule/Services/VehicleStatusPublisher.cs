using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Rules;
using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Modules.MonitoringModule.Services
{
    public class VehicleStatusPublisher : IVehicleStatusPublisher
    {
        private readonly IVehicleStateStore _vehicleStateStore;

        public VehicleStatusPublisher(IVehicleStateStore vehicleStateStore)
        {
            _vehicleStateStore = vehicleStateStore;
        }

        public VehicleStatusIngestionResult PublishStatus(VehicleStatusSnapshot snapshot)
        {
            var reportedAt = snapshot.ReportedAt == default ? DateTime.Now : snapshot.ReportedAt;
            var normalizedSnapshot = new VehicleStatusSnapshot
            {
                VehicleId = snapshot.VehicleId.Trim(),
                Brand = snapshot.Brand.Trim(),
                BatteryLevel = Math.Clamp(snapshot.BatteryLevel, 0, 100),
                Location = snapshot.Location.Trim(),
                State = snapshot.State,
                CurrentTaskId = string.IsNullOrWhiteSpace(snapshot.CurrentTaskId) ? null : snapshot.CurrentTaskId.Trim(),
                ReportedAt = reportedAt,
                LoadState = snapshot.LoadState,
                Position = snapshot.Position ?? new MapPosition(),
                IsOnline = snapshot.IsOnline || snapshot.State != RobotState.Offline,
                IsCharging = snapshot.IsCharging,
                HasAlarm = snapshot.HasAlarm || snapshot.State == RobotState.Fault || !string.IsNullOrWhiteSpace(snapshot.ActiveAlarmCode),
                ActiveAlarmCode = snapshot.ActiveAlarmCode,
                ActiveAlarmMessage = snapshot.ActiveAlarmMessage,
                Telemetry = snapshot.Telemetry == null ? new Dictionary<string, string>() : new Dictionary<string, string>(snapshot.Telemetry)
            };

            _vehicleStateStore.UpsertStatus(normalizedSnapshot);

            var result = new VehicleStatusIngestionResult
            {
                Snapshot = normalizedSnapshot,
                VehicleStatusUpdated = true,
                LowBatteryDetected = VehicleStatusRules.IsLowBattery(normalizedSnapshot.BatteryLevel)
            };

            if (result.LowBatteryDetected)
            {
                result.LowBatteryAlert = new RobotBatteryAlert
                {
                    VehicleId = normalizedSnapshot.VehicleId,
                    Brand = normalizedSnapshot.Brand,
                    BatteryLevel = normalizedSnapshot.BatteryLevel,
                    Location = normalizedSnapshot.Location,
                    Threshold = VehicleStatusRules.LowBatteryThreshold,
                    OccurredAt = normalizedSnapshot.ReportedAt
                };

            }

            return result;
        }
    }
}
