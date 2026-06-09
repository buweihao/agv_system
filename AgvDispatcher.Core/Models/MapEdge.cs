using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class MapEdge
    {
        public string EdgeId { get; set; } = string.Empty;

        public string MapId { get; set; } = string.Empty;

        public string FromNodeId { get; set; } = string.Empty;

        public string ToNodeId { get; set; } = string.Empty;

        public EdgeDirection Direction { get; set; } = EdgeDirection.Bidirectional;

        public double Length { get; set; }

        public double MaxSpeed { get; set; }

        public double TurnAngle { get; set; }

        public int Cost { get; set; } = 1;

        public bool IsEnabled { get; set; } = true;

        public bool IsLocked { get; set; }

        public string? LockedByCommandId { get; set; }

        public string AreaCode { get; set; } = string.Empty;

        public string Remark { get; set; } = string.Empty;
    }
}
