using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Rules;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class VehicleStatusPublisher : IVehicleStatusPublisher
    {
        private const string LowBatteryAlarmCode = "LOW_BATTERY";

        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly IAlarmService _alarmService;
        private readonly IAuditTrailService _auditTrail;

        public VehicleStatusPublisher(
            IVehicleStateStore vehicleStateStore,
            IAlarmService alarmService,
            IAuditTrailService auditTrail)
        {
            _vehicleStateStore = vehicleStateStore;
            _alarmService = alarmService;
            _auditTrail = auditTrail;
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
                ReportedAt = snapshot.ReportedAt == default ? DateTime.Now : snapshot.ReportedAt,
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
            _auditTrail.RecordVehicleStatusUpdated(normalizedSnapshot);

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

                EnsureLowBatteryAlarm(normalizedSnapshot);
            }
            else
            {
                ClearRecoveredLowBatteryAlarm(normalizedSnapshot);
            }

            return result;
        }

        private void EnsureLowBatteryAlarm(VehicleStatusSnapshot snapshot)
        {
            var hasActiveLowBatteryAlarm = _alarmService.QueryAlarms(new AlarmQuery
                {
                    AlarmCode = LowBatteryAlarmCode,
                    VehicleId = snapshot.VehicleId,
                    State = AlarmState.Active,
                    Take = 1
                })
                .Any();

            if (hasActiveLowBatteryAlarm)
            {
                return;
            }

            _alarmService.RaiseAlarm(new AlarmEvent
            {
                AlarmCode = LowBatteryAlarmCode,
                Name = "AGV low battery",
                Description = $"{snapshot.VehicleId} battery is {snapshot.BatteryLevel:0.#}%, below {VehicleStatusRules.LowBatteryThreshold:0.#}%.",
                Severity = AlarmSeverity.Warning,
                SourceType = "Vehicle",
                SourceId = snapshot.VehicleId,
                VehicleId = snapshot.VehicleId,
                LocationNodeId = snapshot.Location,
                OccurredAt = snapshot.ReportedAt,
                Metadata =
                {
                    ["BatteryLevel"] = snapshot.BatteryLevel.ToString("0.#"),
                    ["Threshold"] = VehicleStatusRules.LowBatteryThreshold.ToString("0.#"),
                    ["State"] = snapshot.State.ToString()
                }
            });
        }

        private void ClearRecoveredLowBatteryAlarm(VehicleStatusSnapshot snapshot)
        {
            var activeLowBatteryAlarms = _alarmService.QueryAlarms(new AlarmQuery
            {
                AlarmCode = LowBatteryAlarmCode,
                VehicleId = snapshot.VehicleId,
                State = AlarmState.Active,
                Take = 10
            });

            foreach (var alarm in activeLowBatteryAlarms)
            {
                _alarmService.ClearAlarm(alarm.AlarmId, $"Battery recovered to {snapshot.BatteryLevel:0.#}%.");
            }
        }
    }
}
