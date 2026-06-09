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
    }
}
