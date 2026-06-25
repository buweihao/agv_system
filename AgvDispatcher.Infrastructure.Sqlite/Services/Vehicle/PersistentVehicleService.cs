using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentVehicleService : IVehicleService
    {
        private readonly IVehicleRepository _vehicles;
        private readonly IVehicleStateStore _vehicleStateStore;

        public PersistentVehicleService(IVehicleRepository vehicles, IVehicleStateStore vehicleStateStore)
        {
            _vehicles = vehicles;
            _vehicleStateStore = vehicleStateStore;
        }

        public IReadOnlyList<Vehicle> GetVehicles()
        {
            return _vehicles.GetAllAsync().GetAwaiter().GetResult();
        }

        public Vehicle? GetVehicle(string vehicleId)
        {
            return _vehicles.GetByIdAsync(vehicleId).GetAwaiter().GetResult();
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
            if (vehicle is null)
            {
                return;
            }

            if (!vehicle.IsEnabled)
            {
                _vehicleStateStore.RemoveVehicle(status.VehicleId);
                return;
            }

            _vehicleStateStore.UpsertStatus(new VehicleStatusSnapshot
            {
                VehicleId = status.VehicleId,
                Brand = vehicle.Brand,
                BatteryLevel = status.BatteryLevel,
                Location = status.LocationText,
                State = status.State,
                CurrentTaskId = status.CurrentTaskId,
                ReportedAt = status.ReportedAt == default ? DateTime.Now : status.ReportedAt,
                LoadState = status.LoadState,
                Position = status.Position ?? new MapPosition(),
                IsOnline = status.IsOnline,
                IsCharging = status.IsCharging,
                HasAlarm = status.HasAlarm,
                ActiveAlarmCode = status.ActiveAlarmCode,
                ActiveAlarmMessage = status.ActiveAlarmMessage,
                Telemetry = status.Telemetry == null ? new Dictionary<string, string>() : new Dictionary<string, string>(status.Telemetry)
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
                LoadState = snapshot.LoadState,
                Position = snapshot.Position ?? new MapPosition(),
                IsOnline = snapshot.IsOnline || snapshot.State != RobotState.Offline,
                IsCharging = snapshot.IsCharging || snapshot.Location.StartsWith("Charge", StringComparison.OrdinalIgnoreCase),
                HasAlarm = snapshot.HasAlarm || snapshot.State == RobotState.Fault,
                ActiveAlarmCode = snapshot.ActiveAlarmCode,
                ActiveAlarmMessage = snapshot.ActiveAlarmMessage,
                Telemetry = snapshot.Telemetry == null ? new Dictionary<string, string>() : new Dictionary<string, string>(snapshot.Telemetry)
            };
        }
    }
}
