using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Map
{
    public sealed class MapNodeDto
    {
        public string NodeId { get; init; } = string.Empty;

        public string NodeCode { get; init; } = string.Empty;

        public string NodeName { get; init; } = string.Empty;

        public MapNodeType NodeType { get; init; } = MapNodeType.Normal;

        public double X { get; init; }

        public double Y { get; init; }

        public double? Angle { get; init; }

        public string? AreaId { get; init; }

        public bool Enabled { get; init; } = true;

        public IReadOnlyDictionary<string, string> Properties { get; init; }
            = new Dictionary<string, string>();
    }
}
