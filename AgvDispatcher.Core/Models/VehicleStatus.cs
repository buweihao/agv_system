using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class VehicleStatus
    {
        public string VehicleId { get; set; } = string.Empty;

        public RobotState State { get; set; } = RobotState.Offline;

        public VehicleMode Mode { get; set; } = VehicleMode.Automatic;

        public VehicleLoadState LoadState { get; set; } = VehicleLoadState.Unknown;

        public double BatteryLevel { get; set; }

        public double BatteryVoltage { get; set; }

        public double BatteryCurrent { get; set; }

        public double BatteryTemperature { get; set; }

        public MapPosition Position { get; set; } = new();

        public string LocationText { get; set; } = string.Empty;

        public double Speed { get; set; }

        public double TargetSpeed { get; set; }

        public string? CurrentTaskId { get; set; }

        public string? CurrentCommandId { get; set; }

        public string? TargetNodeId { get; set; }

        public string? CurrentEdgeId { get; set; }

        public int TaskProgressPercent { get; set; }

        public bool IsOnline { get; set; }

        public bool IsCharging { get; set; }

        public bool HasAlarm { get; set; }

        public string? ActiveAlarmCode { get; set; }

        public string? ActiveAlarmMessage { get; set; }

        public DateTime ReportedAt { get; set; } = DateTime.Now;

        public DateTime? LastHeartbeatAt { get; set; }

        public Dictionary<string, string> Telemetry { get; set; } = new();
    }
}
