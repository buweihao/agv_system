using AgvDispatcher.Core.Contracts.Reservations.Enums;

namespace AgvDispatcher.Core.Contracts.Reservations.Models
{
    /// <summary>
    /// Represents the result of acquiring a rolling lock on a route.
    /// 表示在路线上获取滚动锁定的结果。
    /// </summary>
    public sealed class RouteRollingLockResultDto
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
        /// Gets the acquired rolling window, if any.
        /// 获取获取到的滚动窗口（如果有）。
        /// </summary>
        public RouteRollingWindowDto? AcquiredWindow { get; init; }

        /// <summary>
        /// Gets a value indicating whether the lock was successfully acquired.
        /// 获取一个值，指示是否成功获取锁定。
        /// </summary>
        public bool Acquired { get; init; }

        /// <summary>
        /// Gets a value indicating whether the vehicle should wait.
        /// 获取一个值，指示车辆是否应等待。
        /// </summary>
        public bool ShouldWait { get; init; }

        /// <summary>
        /// Gets a value indicating whether a replan is required.
        /// 获取一个值，指示是否需要重新规划。
        /// </summary>
        public bool RequiresReplan { get; init; }

        /// <summary>
        /// Gets the failure reason.
        /// 获取失败原因。
        /// </summary>
        public RouteReservationFailureReason FailureReason { get; init; } = RouteReservationFailureReason.None;

        /// <summary>
        /// Gets an optional message.
        /// 获取可选消息。
        /// </summary>
        public string Message { get; init; } = string.Empty;
    }
}
