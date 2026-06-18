using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    /// <summary>
    /// Represents a request to release passed route resources.
    /// 表示释放已通过的路线资源的请求。
    /// </summary>
    public sealed class ReleasePassedRouteResourcesRequest : IAgvRequest
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
        /// Gets the passed segment sequence.
        /// 获取已通过段的序号。
        /// </summary>
        public int PassedSegmentSequence { get; init; }
    }
}
