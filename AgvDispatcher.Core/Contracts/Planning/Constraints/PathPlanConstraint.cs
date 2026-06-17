using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Planning.Constraints
{
    /// <summary>
    /// 路径规划的动态约束条件。
    /// 这些信息由调度模块从交通管制模块、路径预约模块整理后传入，
    /// 路径规划模块本身不主动查询这些状态。
    /// </summary>
    public sealed class PathPlanConstraint
    {
        /// <summary>
        /// 明确禁行的点。
        /// </summary>
        public IReadOnlySet<string>? ForbiddenNodeIds { get; init; }

        /// <summary>
        /// 明确禁行的边。
        /// </summary>
        public IReadOnlySet<string>? ForbiddenEdgeIds { get; init; }

        /// <summary>
        /// 当前已被车辆占用的点。
        /// </summary>
        public IReadOnlySet<string>? OccupiedNodeIds { get; init; }

        /// <summary>
        /// 当前已被车辆占用的边。
        /// </summary>
        public IReadOnlySet<string>? OccupiedEdgeIds { get; init; }

        /// <summary>
        /// 当前已被其他车辆预约的点。
        /// </summary>
        public IReadOnlySet<string>? ReservedNodeIds { get; init; }

        /// <summary>
        /// 当前已被其他车辆预约的边。
        /// </summary>
        public IReadOnlySet<string>? ReservedEdgeIds { get; init; }

        /// <summary>
        /// 是否避开占用资源。
        /// </summary>
        public bool AvoidOccupiedResources { get; init; } = true;

        /// <summary>
        /// 是否避开已预约资源。
        /// </summary>
        public bool AvoidReservedResources { get; init; } = true;

        /// <summary>
        /// 是否允许倒车或反向通行。
        /// </summary>
        public bool AllowReverse { get; init; } = false;

        /// <summary>
        /// 是否允许临时阻塞资源参与规划。
        /// </summary>
        public bool AllowTemporaryBlockedPass { get; init; } = false;
    }
}
