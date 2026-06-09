namespace AgvDispatcher.Core.Models
{
    public class MapPosition
    {
        public string MapId { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public double Heading { get; set; }

        public string? NodeId { get; set; }

        public string? AreaCode { get; set; }
    }
}
