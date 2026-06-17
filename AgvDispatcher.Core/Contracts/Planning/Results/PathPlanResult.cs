using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Planning.Results
{
    /// <summary>
    /// 路径规划结果。
    /// 路径规划成功时，Segments 中应包含完整路径段。
    /// </summary>
    public sealed class PathPlanResult
    {
        /// <summary>
        /// 规划结果唯一标识。
        /// </summary>
        public string PlanId { get; init; } = string.Empty;

        /// <summary>
        /// 路径起点 NodeId。
        /// </summary>
        public string StartNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 路径终点 NodeId。
        /// </summary>
        public string TargetNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 完整路径段。
        /// </summary>
        public IReadOnlyList<PathSegmentDto>? Segments { get; init; }

        /// <summary>
        /// 总距离。
        /// </summary>
        public double TotalDistance { get; init; }

        /// <summary>
        /// 预计总耗时（秒）。
        /// </summary>
        public double EstimatedSeconds { get; init; }

        /// <summary>
        /// 转弯次数。
        /// </summary>
        public int TurnCount { get; init; }

        /// <summary>
        /// 规划代价。
        /// </summary>
        public PathPlanCost? Cost { get; init; }

        /// <summary>
        /// 规划时参考的地图版本。
        /// </summary>
        public string? MapVersion { get; init; }

        /// <summary>
        /// 是否可达。不可达时，Segments 可能为空，具体失败原因参考 AgvResult 的错误码。
        /// </summary>
        public bool IsReachable { get; init; }

        /// <summary>
        /// 警告信息列表（例如部分约束无法满足但找到次优解等）。
        /// </summary>
        public IReadOnlyList<string>? Warnings { get; init; }
    }
}
