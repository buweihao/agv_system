using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Rules;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
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
            ArgumentNullException.ThrowIfNull(snapshot);

            var normalizedSnapshot = new VehicleStatusSnapshot
            {
                VehicleId = snapshot.VehicleId.Trim(),
                Brand = snapshot.Brand.Trim(),
                BatteryLevel = Math.Clamp(snapshot.BatteryLevel, 0, 100),
                Location = snapshot.Location.Trim(),
                State = snapshot.State,
                CurrentTaskId = string.IsNullOrWhiteSpace(snapshot.CurrentTaskId) ? null : snapshot.CurrentTaskId.Trim(),
                ReportedAt = snapshot.ReportedAt == default ? DateTime.Now : snapshot.ReportedAt
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
