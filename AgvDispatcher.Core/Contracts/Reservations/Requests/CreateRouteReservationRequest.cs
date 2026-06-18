using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Planning.Results;
using AgvDispatcher.Core.Contracts.Reservations.Enums;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    /// <summary>
    /// Represents a request to create a route reservation.
    /// 表示创建路线预留的请求。
    /// </summary>
    public sealed class CreateRouteReservationRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// 获取请求上下文。
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the task identifier.
        /// 获取任务标识符。
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
        /// Gets the path segments.
        /// 获取路径段。
        /// </summary>
        public IReadOnlyList<PathSegmentDto> Segments { get; init; } = Array.Empty<PathSegmentDto>();

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
    }
}
