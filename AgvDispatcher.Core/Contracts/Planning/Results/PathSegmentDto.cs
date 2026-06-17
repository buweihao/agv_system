using AgvDispatcher.Core.Contracts.Planning.Enums;

namespace AgvDispatcher.Core.Contracts.Planning.Results
{
    /// <summary>
    /// 路径段信息。
    /// 包含边信息，用于后续路径预约、滚动锁、交通管制。
    /// </summary>
    public sealed class PathSegmentDto
    {
        /// <summary>
        /// 路径段的顺序。
        /// </summary>
        public int Sequence { get; init; }

        /// <summary>
        /// 起点 NodeId。
        /// </summary>
        public string FromNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 终点 NodeId。
        /// </summary>
        public string ToNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 经过的 EdgeId。
        /// </summary>
        public string EdgeId { get; init; } = string.Empty;

        /// <summary>
        /// 路径段距离。
        /// </summary>
        public double Distance { get; init; }

        /// <summary>
        /// 预计耗时（秒）。
        /// </summary>
        public double EstimatedSeconds { get; init; }

        /// <summary>
        /// 运动方向。
        /// </summary>
        public MoveDirection Direction { get; init; } = MoveDirection.Unknown;

        /// <summary>
        /// 是否为反向运动。
        /// </summary>
        public bool IsReverseMove { get; init; }

        /// <summary>
        /// 速度限制（可选）。
        /// </summary>
        public double? SpeedLimit { get; init; }
    }
}
