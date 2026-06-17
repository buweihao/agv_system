using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Map
{
    public sealed class MapSnapshotDto
    {
        public string MapId { get; init; } = string.Empty;

        public string MapName { get; init; } = string.Empty;

        public string Version { get; init; } = string.Empty;

        public IReadOnlyList<MapNodeDto> Nodes { get; init; } = Array.Empty<MapNodeDto>();

        public IReadOnlyList<MapEdgeDto> Edges { get; init; } = Array.Empty<MapEdgeDto>();

        public IReadOnlyList<MapAreaDto> Areas { get; init; } = Array.Empty<MapAreaDto>();

        public IReadOnlyList<VendorNodeMappingDto> VendorNodeMappings { get; init; }
            = Array.Empty<VendorNodeMappingDto>();

        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
    }
}
