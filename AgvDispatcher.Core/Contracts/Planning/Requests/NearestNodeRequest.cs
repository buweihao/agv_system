using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Planning.Constraints;

namespace AgvDispatcher.Core.Contracts.Planning.Requests
{
    /// <summary>
    /// 最近可达点请求。
    /// 用于从候选点中寻找离起点最近的可达点。
    /// </summary>
    public sealed class NearestNodeRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// 当前地图快照。
        /// </summary>
        public MapSnapshotDto? MapSnapshot { get; init; }

        /// <summary>
        /// 车辆 ID（可选）。
        /// </summary>
        public string? VehicleId { get; init; }

        /// <summary>
        /// 起点 NodeId。
        /// </summary>
        public string FromNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 候选的终点 NodeId 列表。
        /// </summary>
        public IReadOnlyList<string> CandidateNodeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 当前规划约束条件。
        /// </summary>
        public PathPlanConstraint Constraint { get; init; } = new PathPlanConstraint();
    }
}
