using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    /// <summary>
    /// Represents a request to release a route reservation.
    /// 表示释放路线预留的请求。
    /// </summary>
    public sealed class ReleaseRouteReservationRequest : IAgvRequest
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

        /// <summary>
        /// Gets the release reason.
        /// 获取释放原因。
        /// </summary>
        public string Reason { get; init; } = string.Empty;
    }
}
