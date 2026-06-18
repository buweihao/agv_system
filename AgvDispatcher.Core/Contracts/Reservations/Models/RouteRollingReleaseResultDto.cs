using AgvDispatcher.Core.Contracts.Reservations.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Reservations.Models
{
    /// <summary>
    /// Represents the result of releasing passed resources in a rolling window.
    /// 表示在滚动窗口中释放已通过资源的结果。
    /// </summary>
    public sealed class RouteRollingReleaseResultDto
    {
        /// <summary>
        /// Gets the reservation identifier.
        /// 获取预留标识符。
        /// </summary>
        public string ReservationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the current reservation state.
        /// 获取当前预留状态。
        /// </summary>
        public RouteReservationState State { get; init; }

        /// <summary>
        /// Gets the sequences of the released segments.
        /// 获取已释放段的序号。
        /// </summary>
        public IReadOnlyList<int> ReleasedSegmentSequences { get; init; } = Array.Empty<int>();

        /// <summary>
        /// Gets the traffic resources that were released.
        /// 获取已释放的交通资源。
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> ReleasedResources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the current rolling window after release.
        /// 获取释放后的当前滚动窗口。
        /// </summary>
        public RouteRollingWindowDto? CurrentWindow { get; init; }
    }
}
