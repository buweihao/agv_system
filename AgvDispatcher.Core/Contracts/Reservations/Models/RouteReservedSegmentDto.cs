using AgvDispatcher.Core.Contracts.Planning.Results;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Reservations.Models
{
    /// <summary>
    /// Represents a reserved segment within a route reservation.
    /// 表示路线预留中的一个保留段。
    /// </summary>
    public sealed class RouteReservedSegmentDto
    {
        /// <summary>
        /// Gets the path segment.
        /// 获取路径段。
        /// </summary>
        public PathSegmentDto Segment { get; init; } = new PathSegmentDto();

        /// <summary>
        /// Gets the traffic resources associated with this segment.
        /// 获取与此段相关的交通资源。
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the traffic reservation identifiers.
        /// 获取交通预留标识符。
        /// </summary>
        public IReadOnlyList<string> TrafficReservationIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets a value indicating whether the segment is reserved.
        /// 获取一个值，指示该段是否已被预留。
        /// </summary>
        public bool IsReserved { get; init; }

        /// <summary>
        /// Gets a value indicating whether the segment is locked.
        /// 获取一个值，指示该段是否已被锁定。
        /// </summary>
        public bool IsLocked { get; init; }

        /// <summary>
        /// Gets a value indicating whether the segment is released.
        /// 获取一个值，指示该段是否已被释放。
        /// </summary>
        public bool IsReleased { get; init; }

        /// <summary>
        /// Gets the time when the segment was locked.
        /// 获取段锁定时间。
        /// </summary>
        public DateTimeOffset? LockedAt { get; init; }

        /// <summary>
        /// Gets the time when the segment was released.
        /// 获取段释放时间。
        /// </summary>
        public DateTimeOffset? ReleasedAt { get; init; }
    }
}
