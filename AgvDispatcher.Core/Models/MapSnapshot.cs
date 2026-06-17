using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Models
{
    /// <summary>
    /// 地图快照。
    /// </summary>
    /// <remarks>
    /// 相对完整、不可随意修改的地图视图。
    /// 包含当前地图的点位、路线、版本号和发布时间等信息。
    /// </remarks>
    public class MapSnapshot
    {
        /// <summary>
        /// 地图编号。
        /// </summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>
        /// 地图版本。
        /// </summary>
        public long Version { get; init; }

        /// <summary>
        /// 发布时间。
        /// </summary>
        public DateTime PublishedAt { get; init; }

        /// <summary>
        /// 地图中的所有点位。
        /// </summary>
        public IReadOnlyList<MapNode> Nodes { get; init; } = Array.Empty<MapNode>();

        /// <summary>
        /// 地图中的所有路线。
        /// </summary>
        public IReadOnlyList<MapEdge> Edges { get; init; } = Array.Empty<MapEdge>();
    }
}
