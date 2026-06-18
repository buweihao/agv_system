using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests removal of a manual or operational block from traffic resources.
    /// 请求从交通资源中移除手动或操作性阻塞。
    /// </summary>
    public sealed class TrafficUnblockRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// 获取请求上下文。
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the resources to unblock.
        /// 获取要解除阻塞的资源。
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the unblock reason.
        /// 获取解除阻塞原因。
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the operator identifier that requested the unblock, if any.
        /// 获取请求解除阻塞的操作员标识符（如果有）。
        /// </summary>
        public string? OperatorId { get; init; }
    }
}
