using System;
using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    /// <summary>
    /// 旧版地图边模型。
    /// 跨模块接口请使用 <see cref="AgvDispatcher.Core.Contracts.Map.MapEdgeDto"/>。
    /// </summary>
    [Obsolete("跨模块接口请使用 AgvDispatcher.Core.Contracts.Map.MapEdgeDto。本模型仅保留给旧代码逐步迁移使用。", false)]
    public class MapEdge
    {
        public string EdgeId { get; set; } = string.Empty;

        public string MapId { get; set; } = string.Empty;

        public string MapVersion { get; set; } = "v1";

        public string FromNodeId { get; set; } = string.Empty;

        public string ToNodeId { get; set; } = string.Empty;

        public EdgeDirection Direction { get; set; } = EdgeDirection.Bidirectional;

        public double Length { get; set; }

        public double MaxSpeed { get; set; }

        public double TurnAngle { get; set; }

        public int Cost { get; set; } = 1;

        public bool IsEnabled { get; set; } = true;

        public string AreaCode { get; set; } = string.Empty;

        public string AllowedBrands { get; set; } = string.Empty;

        public int MaxVehicleFlow { get; set; } = 1;

        public string Remark { get; set; } = string.Empty;
    }
}
