namespace AgvDispatcher.Core.Models
{
    public class RobotBatteryAlert
    {
        public string VehicleId { get; set; } = string.Empty;

        public string Brand { get; set; } = string.Empty;

        public double BatteryLevel { get; set; }

        public string Location { get; set; } = string.Empty;

        public double Threshold { get; set; }

        public DateTime OccurredAt { get; set; }
    }
}
