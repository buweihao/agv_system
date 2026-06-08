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

        public string? CurrentTaskId { get; set; }

        public DateTime ReportedAt { get; set; }
    }
}
