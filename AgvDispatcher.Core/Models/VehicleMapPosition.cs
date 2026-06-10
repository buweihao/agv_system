namespace AgvDispatcher.Core.Models
{
    public class VehicleMapPosition
    {
        public string VehicleId { get; set; } = string.Empty;

        public string NodeId { get; set; } = string.Empty;

        public MapPosition Position { get; set; } = new();

        public double BatteryLevel { get; set; }

        public string StateText { get; set; } = string.Empty;

        public string Color { get; set; } = "#00BFFF";
    }
}
