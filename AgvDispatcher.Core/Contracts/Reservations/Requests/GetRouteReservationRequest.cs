using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    /// <summary>
    /// Represents a request to get a route reservation.
    /// 表示获取路线预留的请求。
    /// </summary>
    public sealed class GetRouteReservationRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// 获取请求上下文。
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the reservation identifier.
        /// 获取预留标识符。
        /// </summary>
        public string ReservationId { get; init; } = string.Empty;
    }
}
