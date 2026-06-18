using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    /// <summary>
    /// Represents a request to get route reservations by vehicle.
    /// 表示按车辆获取路线预留的请求。
    /// </summary>
    public sealed class GetVehicleRouteReservationsRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// 获取请求上下文。
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the vehicle identifier.
        /// 获取车辆标识符。
        /// </summary>
        public string VehicleId { get; init; } = string.Empty;
    }
}
