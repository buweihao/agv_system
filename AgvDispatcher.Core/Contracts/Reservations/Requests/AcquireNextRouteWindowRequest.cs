using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    /// <summary>
    /// Represents a request to acquire the next route rolling window.
    /// 表示获取下一个路线滚动窗口的请求。
    /// </summary>
    public sealed class AcquireNextRouteWindowRequest : IAgvRequest
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
        /// Gets the current node identifier.
        /// 获取当前节点标识符。
        /// </summary>
        public string CurrentNodeId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the current segment sequence.
        /// 获取当前段序号。
        /// </summary>
        public int CurrentSegmentSequence { get; init; }

        /// <summary>
        /// Gets the rolling window size.
        /// 获取滚动窗口大小。
        /// </summary>
        public int RollingWindowSize { get; init; }
    }
}
