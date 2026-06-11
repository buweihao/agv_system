using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Rules;
using Prism.Events;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentVehicleStateStore : IVehicleStateStore
    {
        private readonly Dictionary<string, VehicleStatusSnapshot> _vehicles = new(StringComparer.OrdinalIgnoreCase);
        private readonly IEventAggregator _eventAggregator;
        private readonly IVehicleRepository _vehicleRepository;
        private readonly object _syncRoot = new();

        public PersistentVehicleStateStore(IEventAggregator eventAggregator, IVehicleRepository vehicleRepository)
        {
            _eventAggregator = eventAggregator;
            _vehicleRepository = vehicleRepository;

            SeedFromConfiguration();
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

        private void SeedFromConfiguration()
        {
            var vehicles = _vehicleRepository.GetAllAsync().GetAwaiter().GetResult();
            var now = DateTime.Now;

            foreach (var vehicle in vehicles.Where(vehicle => vehicle.IsEnabled))
            {
                var snapshot = new VehicleStatusSnapshot
                {
                    VehicleId = vehicle.VehicleId,
                    Brand = vehicle.Brand,
                    State = RobotState.Offline,
                    Location = string.IsNullOrWhiteSpace(vehicle.AreaCode) ? "Unassigned" : vehicle.AreaCode,
                    BatteryLevel = 100,
                    CurrentTaskId = null,
                    ReportedAt = now
                };

                lock (_syncRoot)
                {
                    _vehicles[snapshot.VehicleId] = snapshot;
                }
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
                    return;
                }
            }

            UpsertStatus(new VehicleStatusSnapshot
            {
                VehicleId = vehicle.VehicleId,
                Brand = vehicle.Brand,
                State = RobotState.Offline,
                Location = string.IsNullOrWhiteSpace(vehicle.AreaCode) ? "Unassigned" : vehicle.AreaCode,
                BatteryLevel = 100,
                ReportedAt = DateTime.Now
            });
        }
    }
}
