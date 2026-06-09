using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockVehicleService : IVehicleService
    {
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly IReadOnlyList<Vehicle> _vehicles = MockData.CreateVehicles();

        public MockVehicleService(IVehicleStateStore vehicleStateStore)
        {
            _vehicleStateStore = vehicleStateStore;
        }

        public IReadOnlyList<Vehicle> GetVehicles()
        {
            return _vehicles;
        }

        public Vehicle? GetVehicle(string vehicleId)
        {
            return _vehicles.FirstOrDefault(vehicle =>
                string.Equals(vehicle.VehicleId, vehicleId, StringComparison.OrdinalIgnoreCase));
        }

        public VehicleStatus? GetVehicleStatus(string vehicleId)
        {
            var snapshot = _vehicleStateStore.GetVehicle(vehicleId);
            return snapshot is null ? null : ToVehicleStatus(snapshot);
        }

        public IReadOnlyList<VehicleStatus> GetVehicleStatuses()
        {
            return _vehicleStateStore.GetAllVehicles()
                .Select(ToVehicleStatus)
                .ToArray();
        }

        public void UpdateVehicleStatus(VehicleStatus status)
        {
            ArgumentNullException.ThrowIfNull(status);

            var vehicle = GetVehicle(status.VehicleId);
            _vehicleStateStore.UpsertStatus(new VehicleStatusSnapshot
            {
                VehicleId = status.VehicleId,
                Brand = vehicle?.Brand ?? string.Empty,
                BatteryLevel = status.BatteryLevel,
                Location = status.LocationText,
                State = status.State,
                CurrentTaskId = status.CurrentTaskId,
                ReportedAt = status.ReportedAt == default ? DateTime.Now : status.ReportedAt
            });
        }

        public bool IsVehicleAvailable(string vehicleId)
        {
            var status = GetVehicleStatus(vehicleId);
            return status is { State: RobotState.Idle, IsOnline: true, HasAlarm: false };
        }

        private static VehicleStatus ToVehicleStatus(VehicleStatusSnapshot snapshot)
        {
            return new VehicleStatus
            {
                VehicleId = snapshot.VehicleId,
                State = snapshot.State,
                BatteryLevel = snapshot.BatteryLevel,
                LocationText = snapshot.Location,
                CurrentTaskId = snapshot.CurrentTaskId,
                ReportedAt = snapshot.ReportedAt,
                LastHeartbeatAt = snapshot.ReportedAt,
                IsOnline = snapshot.State != RobotState.Offline,
                IsCharging = string.Equals(snapshot.Location, "Charge", StringComparison.OrdinalIgnoreCase),
                HasAlarm = snapshot.State == RobotState.Fault
            };
        }
    }
}
