using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    /// <summary>
    /// 旧版地图节点模型。
    /// 跨模块接口请使用 <see cref="AgvDispatcher.Core.Contracts.Map.MapNodeDto"/>。
    /// </summary>
    [Obsolete("跨模块接口请使用 AgvDispatcher.Core.Contracts.Map.MapNodeDto。本模型仅保留给旧代码逐步迁移使用。", false)]
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

        public int ParkingCapacity { get; set; } = 1;

        public string AllowedBrands { get; set; } = string.Empty;

        public VehicleCapability RequiredCapabilities { get; set; } = VehicleCapability.None;

        public Dictionary<string, string> Tags { get; set; } = new();
    }
}
