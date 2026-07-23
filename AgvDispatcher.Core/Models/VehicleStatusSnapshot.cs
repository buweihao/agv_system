using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class VehicleStatusSnapshot
    {
        public string VehicleId { get; set; } = string.Empty;

        public string Brand { get; set; } = string.Empty;

        public double BatteryLevel { get; set; }

        public string Location { get; set; } = string.Empty;

        public RobotState State { get; set; }

        public VehicleLoadState LoadState { get; set; } = VehicleLoadState.Unknown;

        public MapPosition Position { get; set; } = new();

        public string? CurrentTaskId { get; set; }

        public bool IsOnline { get; set; }

        public bool IsCharging { get; set; }

        public bool HasAlarm { get; set; }

        public string? ActiveAlarmCode { get; set; }

        public string? ActiveAlarmMessage { get; set; }

        public DateTime ReportedAt { get; set; }

        public Dictionary<string, string> Telemetry { get; set; } = new();

        public VehicleStatusSnapshot Clone() => new()
        {
            VehicleId = VehicleId,
            Brand = Brand,
            BatteryLevel = BatteryLevel,
            Location = Location,
            State = State,
            LoadState = LoadState,
            Position = Position?.Clone() ?? new MapPosition(),
            CurrentTaskId = CurrentTaskId,
            IsOnline = IsOnline,
            IsCharging = IsCharging,
            HasAlarm = HasAlarm,
            ActiveAlarmCode = ActiveAlarmCode,
            ActiveAlarmMessage = ActiveAlarmMessage,
            ReportedAt = ReportedAt,
            Telemetry = new Dictionary<string, string>(
                Telemetry ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase)
        };
    }
}
