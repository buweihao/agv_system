namespace AgvDispatcher.Core.Contracts.Reservations.Models
{
    /// <summary>
    /// Represents a rolling window of active segments.
    /// 表示活动段的滚动窗口。
    /// </summary>
    public sealed class RouteRollingWindowDto
    {
        /// <summary>
        /// Gets the start segment sequence.
        /// 获取起始段序号。
        /// </summary>
        public int StartSegmentSequence { get; init; }

        /// <summary>
        /// Gets the end segment sequence.
        /// 获取结束段序号。
        /// </summary>
        public int EndSegmentSequence { get; init; }

        /// <summary>
        /// Gets the segments within the window.
        /// 获取窗口内的段。
        /// </summary>
        public IReadOnlyList<RouteReservedSegmentDto> Segments { get; init; } = Array.Empty<RouteReservedSegmentDto>();

        /// <summary>
        /// Gets a value indicating whether the complete path is locked.
        /// 获取一个值，指示是否锁定了完整路径。
        /// </summary>
        public bool IsCompletePathLocked { get; init; }
    }
}
