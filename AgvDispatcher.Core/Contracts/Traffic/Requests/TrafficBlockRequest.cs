using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests a manual or operational block for traffic resources.
    /// 请求对交通资源进行手动或操作性阻塞。
    /// </summary>
    public sealed class TrafficBlockRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// 获取请求上下文。
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the resources to block.
        /// 获取要阻塞的资源。
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the block reason.
        /// 获取阻塞原因。
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the operator identifier that requested the block, if any.
        /// 获取请求阻塞的操作员标识符（如果有）。
        /// </summary>
        public string? OperatorId { get; init; }

        /// <summary>
        /// Gets the time-to-live for the block, if any.
        /// 获取阻塞的生存时间（如果有）。
        /// </summary>
        public TimeSpan? Ttl { get; init; }
    }
}
