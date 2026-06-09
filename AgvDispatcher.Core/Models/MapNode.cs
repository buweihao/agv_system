using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class MapNode
    {
        public string NodeId { get; set; } = string.Empty;

        public string MapId { get; set; } = string.Empty;

        public string NodeCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public MapNodeType NodeType { get; set; } = MapNodeType.Normal;

        public MapPosition Position { get; set; } = new();

        public double Heading { get; set; }

        public string AreaCode { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public bool IsOccupied { get; set; }

        public string? OccupiedByVehicleId { get; set; }

        public int ParkingCapacity { get; set; } = 1;

        public Dictionary<string, string> Tags { get; set; } = new();
    }
}
