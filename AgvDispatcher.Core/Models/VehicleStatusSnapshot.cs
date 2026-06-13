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
    }
}
