using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Map
{
    public sealed class MapAreaDto
    {
        public string AreaId { get; init; } = string.Empty;

        public string AreaName { get; init; } = string.Empty;

        public MapAreaType AreaType { get; init; } = MapAreaType.Normal;

        public IReadOnlyList<MapPointDto> BoundaryPoints { get; init; }
            = Array.Empty<MapPointDto>();

        public bool Enabled { get; init; } = true;

        public IReadOnlyDictionary<string, string> Properties { get; init; }
            = new Dictionary<string, string>();
    }
}
