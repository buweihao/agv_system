using AgvDispatcher.Core.Contracts.Reservations.Enums;

namespace AgvDispatcher.Core.Contracts.Reservations.Models
{
    /// <summary>
    /// Represents a route reservation for a vehicle or task.
    /// 表示车辆或任务的路线预留。
    /// </summary>
    public sealed class RouteReservationDto
    {
        /// <summary>
        /// Gets the reservation identifier.
        /// 获取预留标识符。
        /// </summary>
        public string ReservationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the task identifier associated with this reservation.
        /// 获取与此预留相关的任务标识符。
        /// </summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the vehicle identifier.
        /// 获取车辆标识符。
        /// </summary>
        public string VehicleId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the plan identifier.
        /// 获取计划标识符。
        /// </summary>
        public string PlanId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the map identifier.
        /// 获取地图标识符。
        /// </summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the map version.
        /// 获取地图版本。
        /// </summary>
        public string MapVersion { get; init; } = string.Empty;

        /// <summary>
        /// Gets the rolling window size.
        /// 获取滚动窗口大小。
        /// </summary>
        public int RollingWindowSize { get; init; }

        /// <summary>
        /// Gets the reservation policy.
        /// 获取预留策略。
        /// </summary>
        public RouteReservationPolicy ReservationPolicy { get; init; } = RouteReservationPolicy.RollingWindow;

        /// <summary>
        /// Gets the current reservation state.
        /// 获取当前预留状态。
        /// </summary>
        public RouteReservationState State { get; init; } = RouteReservationState.Created;

        /// <summary>
        /// Gets the reserved route segments.
        /// 获取预留的路线段。
        /// </summary>
        public IReadOnlyList<RouteReservedSegmentDto> Segments { get; init; } = Array.Empty<RouteReservedSegmentDto>();

        /// <summary>
        /// Gets the current rolling window, if any.
        /// 获取当前的滚动窗口（如果有）。
        /// </summary>
        public RouteRollingWindowDto? CurrentWindow { get; init; }

        /// <summary>
        /// Gets the time when the reservation was created.
        /// 获取预留创建时间。
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the time when the reservation was last updated.
        /// 获取预留最后更新时间。
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the time when the reservation was released.
        /// 获取预留释放时间。
        /// </summary>
        public DateTimeOffset? ReleasedAt { get; init; }
    }
}
