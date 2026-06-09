using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Rules;
using Prism.Events;

namespace AgvDispatcher.Modules.MonitoringModule.Services
{
    public class VehicleStatusPublisher : IVehicleStatusPublisher
    {
        private readonly IEventAggregator _eventAggregator;

        public VehicleStatusPublisher(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
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
                ReportedAt = reportedAt
            };

            _eventAggregator.GetEvent<VehicleStatusUpdatedEvent>().Publish(normalizedSnapshot);

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

                _eventAggregator.GetEvent<RobotLowBatteryEvent>().Publish(result.LowBatteryAlert);
            }

            return result;
        }
    }
}
