using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Rules;
using Prism.Events;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockVehicleStateStore : IVehicleStateStore
    {
        private readonly Dictionary<string, VehicleStatusSnapshot> _vehicles = new(StringComparer.OrdinalIgnoreCase);
        private readonly IEventAggregator _eventAggregator;
        private readonly object _syncRoot = new();

        public MockVehicleStateStore(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            foreach (var snapshot in MockData.CreateVehicleSnapshots(DateTime.Now))
            {
                CreateVehicle(snapshot);
            }

            _eventAggregator.GetEvent<VehicleConfigurationChangedEvent>().Subscribe(ApplyVehicleConfigurationChange);
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
                return _vehicles.TryGetValue(vehicleId, out var snapshot) ? snapshot : null;
            }
        }

        public IReadOnlyCollection<VehicleStatusSnapshot> GetAllVehicles()
        {
            lock (_syncRoot)
            {
                return _vehicles.Values.ToArray();
            }
        }

        private void ApplyVehicleConfigurationChange(VehicleConfigurationChangedMessage message)
        {
            var vehicle = message.Vehicle;
            if (string.IsNullOrWhiteSpace(vehicle.VehicleId))
            {
                return;
            }

            if (!vehicle.IsEnabled)
            {
                RemoveVehicle(vehicle.VehicleId);
                return;
            }

            lock (_syncRoot)
            {
                if (_vehicles.TryGetValue(vehicle.VehicleId, out var snapshot))
                {
                    snapshot.Brand = vehicle.Brand;
                    snapshot.Location = ResolveConfiguredLocation(vehicle);
                    snapshot.Position ??= new MapPosition();
                    snapshot.Position.MapId = "MAIN";
                    snapshot.Position.NodeId = snapshot.Location;
                    snapshot.Position.AreaCode = vehicle.AreaCode;
                    snapshot.ReportedAt = DateTime.Now;
                    _eventAggregator.GetEvent<VehicleStatusUpdatedEvent>().Publish(snapshot);
                    _eventAggregator.GetEvent<VehicleStateChangedEvent>().Publish(new VehicleStateChangedMessage
                    {
                        ChangeType = VehicleStateChangeType.Updated,
                        Snapshot = snapshot,
                        OccurredAt = DateTime.Now
                    });
                    return;
                }
            }

            UpsertStatus(new VehicleStatusSnapshot
            {
                VehicleId = vehicle.VehicleId,
                Brand = vehicle.Brand,
                State = RobotState.Idle,
                Location = ResolveConfiguredLocation(vehicle),
                BatteryLevel = 100,
                ReportedAt = DateTime.Now,
                Position = new MapPosition
                {
                    MapId = "MAIN",
                    NodeId = ResolveConfiguredLocation(vehicle),
                    AreaCode = vehicle.AreaCode
                }
            });
        }

        private static string ResolveConfiguredLocation(Vehicle vehicle)
        {
            return !string.IsNullOrWhiteSpace(vehicle.HomeNodeId)
                ? vehicle.HomeNodeId
                : string.IsNullOrWhiteSpace(vehicle.AreaCode) ? "Unassigned" : vehicle.AreaCode;
        }
    }
}
