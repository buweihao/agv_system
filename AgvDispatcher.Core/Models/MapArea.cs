using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Map;

namespace AgvDispatcher.Core.Models
{
    public class MapArea
    {
        public string AreaId { get; set; } = string.Empty;

        public string MapId { get; set; } = string.Empty;

        public string MapVersion { get; set; } = "v1";

        public string AreaName { get; set; } = string.Empty;

        public MapAreaType AreaType { get; set; } = MapAreaType.Normal;

        public bool IsEnabled { get; set; } = true;

        public string BoundaryJson { get; set; } = "[]";

        public Dictionary<string, string> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
