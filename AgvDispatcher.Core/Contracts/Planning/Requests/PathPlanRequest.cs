using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Planning.Constraints;
using AgvDispatcher.Core.Contracts.Planning.Enums;

namespace AgvDispatcher.Core.Contracts.Planning.Requests
{
    /// <summary>
    /// 路径规划请求。
    /// </summary>
    public sealed class PathPlanRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// 当前地图快照。
        /// 路径规划需要基于明确的快照，不主动查询。
        /// </summary>
        public MapSnapshotDto? MapSnapshot { get; init; }

        /// <summary>
        /// 车辆 ID（可选）。
        /// 有些规划不绑定具体车辆（例如假设性查询）。
        /// </summary>
        public string? VehicleId { get; init; }

        /// <summary>
        /// 路径起点 NodeId。
        /// </summary>
        public string StartNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 路径终点 NodeId。
        /// </summary>
        public string TargetNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 必须途经的节点 NodeId 列表。
        /// </summary>
        public IReadOnlyList<string> WaypointNodeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 路径规划模式。
        /// </summary>
        public PathPlanMode Mode { get; init; } = PathPlanMode.Balanced;

        /// <summary>
        /// 当前规划约束条件。
        /// </summary>
        public PathPlanConstraint Constraint { get; init; } = new PathPlanConstraint();

        /// <summary>
        /// 需要返回的最大备选路径数量（默认为 1）。
        /// </summary>
        public int MaxAlternativeCount { get; init; } = 1;
    }
}
