namespace AgvDispatcher.Core.Models
{
    public class VehicleStatusIngestionResult
    {
        public VehicleStatusSnapshot Snapshot { get; set; } = new();

        public bool VehicleStatusUpdated { get; set; }

        public bool LowBatteryDetected { get; set; }

        public RobotBatteryAlert? LowBatteryAlert { get; set; }
    }
}
