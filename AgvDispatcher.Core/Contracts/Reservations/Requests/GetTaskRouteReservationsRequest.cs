using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    /// <summary>
    /// Represents a request to get route reservations by task.
    /// 表示按任务获取路线预留的请求。
    /// </summary>
    public sealed class GetTaskRouteReservationsRequest : IAgvRequest
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
    }
}
