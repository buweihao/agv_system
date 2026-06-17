using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Planning.Constraints;

namespace AgvDispatcher.Core.Contracts.Planning.Requests
{
    /// <summary>
    /// 路径可达性检查请求。
    /// 用于判断起点到目标点是否可达。
    /// </summary>
    public sealed class PathReachabilityRequest : IAgvRequest
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
        public string StartNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 终点 NodeId。
        /// </summary>
        public string TargetNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 当前规划约束条件。
        /// </summary>
        public PathPlanConstraint Constraint { get; init; } = new PathPlanConstraint();
    }
}
