using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Map
{
    public sealed class MapEdgeDto
    {
        public string EdgeId { get; init; } = string.Empty;

        public string FromNodeId { get; init; } = string.Empty;

        public string ToNodeId { get; init; } = string.Empty;

        public double Distance { get; init; }

        public MapEdgeDirection Direction { get; init; } = MapEdgeDirection.Bidirectional;

        public MapEdgeType EdgeType { get; init; } = MapEdgeType.Normal;

        public double Cost { get; init; } = 1.0;

        public double? SpeedLimit { get; init; }

        public string? AreaId { get; init; }

        public bool Enabled { get; init; } = true;

        public IReadOnlyDictionary<string, string> Properties { get; init; }
            = new Dictionary<string, string>();
    }
}
