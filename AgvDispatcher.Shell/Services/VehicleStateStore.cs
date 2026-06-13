using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Rules;
using Prism.Events;

namespace AgvDispatcher.Shell.Services
{
    public class VehicleStateStore : IVehicleStateStore
    {
        private readonly Dictionary<string, VehicleStatusSnapshot> _vehicles = new();
        private readonly IEventAggregator _eventAggregator;
        private readonly object _syncRoot = new();

        public VehicleStateStore(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            SeedInitialVehicles();
        }

        public bool CreateVehicle(VehicleStatusSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (string.IsNullOrWhiteSpace(snapshot.VehicleId))
            {
                throw new ArgumentException("VehicleId cannot be empty.", nameof(snapshot));
            }

            lock (_syncRoot)
            {
                if (_vehicles.ContainsKey(snapshot.VehicleId))
                {
                    return false;
                }
            }

            UpsertStatus(snapshot);
            return true;
        }

        public void UpsertStatus(VehicleStatusSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (string.IsNullOrWhiteSpace(snapshot.VehicleId))
            {
                throw new ArgumentException("VehicleId cannot be empty.", nameof(snapshot));
            }

            if (snapshot.Position is null)
            {
                snapshot.Position = new MapPosition();
            }

            if (snapshot.Telemetry is null)
            {
                snapshot.Telemetry = new Dictionary<string, string>();
            }

            if (snapshot.ReportedAt == default)
            {
                snapshot.ReportedAt = DateTime.Now;
            }

            VehicleStateChangeType changeType;
            lock (_syncRoot)
            {
                changeType = _vehicles.ContainsKey(snapshot.VehicleId)
                    ? VehicleStateChangeType.Updated
                    : VehicleStateChangeType.Added;

                _vehicles[snapshot.VehicleId] = snapshot;
            }

            _eventAggregator.GetEvent<VehicleStatusUpdatedEvent>().Publish(snapshot);

            if (VehicleStatusRules.IsLowBattery(snapshot.BatteryLevel))
            {
                _eventAggregator.GetEvent<RobotLowBatteryEvent>().Publish(new RobotBatteryAlert
                {
                    VehicleId = snapshot.VehicleId,
                    Brand = snapshot.Brand,
                    BatteryLevel = snapshot.BatteryLevel,
                    Location = snapshot.Location,
                    Threshold = VehicleStatusRules.LowBatteryThreshold,
                    OccurredAt = DateTime.Now
                });
            }

            _eventAggregator.GetEvent<VehicleStateChangedEvent>().Publish(new VehicleStateChangedMessage
            {
                ChangeType = changeType,
                Snapshot = snapshot,
                OccurredAt = DateTime.Now
            });
        }

        public bool RemoveVehicle(string vehicleId)
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return false;
            }

            bool removed;
            lock (_syncRoot)
            {
                removed = _vehicles.Remove(vehicleId);
            }

            if (removed)
            {
                _eventAggregator.GetEvent<VehicleStateChangedEvent>().Publish(new VehicleStateChangedMessage
                {
                    ChangeType = VehicleStateChangeType.Removed,
                    RemovedVehicleId = vehicleId,
                    OccurredAt = DateTime.Now
                });
            }

            return removed;
        }

        public VehicleStatusSnapshot? GetVehicle(string vehicleId)
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return null;
            }

            lock (_syncRoot)
            {
                return _vehicles.TryGetValue(vehicleId, out var snapshot)
                    ? snapshot
                    : null;
            }
        }

        public IReadOnlyCollection<VehicleStatusSnapshot> GetAllVehicles()
        {
            lock (_syncRoot)
            {
                return _vehicles.Values.ToArray();
            }
        }

        private void SeedInitialVehicles()
        {
            var reportedAt = DateTime.Now;

            var snapshots = new[]
            {
                new VehicleStatusSnapshot { VehicleId = "AGV-002", Brand = "RGV-A", State = RobotState.Running, CurrentTaskId = "TASK20240112001", Location = "A01-02", BatteryLevel = 18, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-003", Brand = "RGV-A", State = RobotState.Running, CurrentTaskId = "TASK20240112002", Location = "A02-08", BatteryLevel = 65, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-008", Brand = "RGV-B", State = RobotState.Fault, CurrentTaskId = null, Location = "B01-05", BatteryLevel = 12, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-010", Brand = "RGV-B", State = RobotState.Running, CurrentTaskId = "TASK20240112003", Location = "A03-01", BatteryLevel = 80, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-017", Brand = "RGV-C", State = RobotState.Idle, CurrentTaskId = null, Location = "Charge-03", BatteryLevel = 92, ReportedAt = reportedAt }
            };

            foreach (var snapshot in snapshots)
            {
                CreateVehicle(snapshot);
            }
        }
    }
}
