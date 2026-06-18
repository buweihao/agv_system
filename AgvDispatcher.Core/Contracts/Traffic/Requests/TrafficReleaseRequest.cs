using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests release of traffic resources associated with a reservation, AGV, or task.
    /// 请求释放与预留、AGV 或任务相关的交通资源。
    /// </summary>
    public sealed class TrafficReleaseRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// 获取请求上下文。
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the reservation identifier to release, if any.
        /// 获取要释放的预留标识符（如果有）。
        /// </summary>
        public string? ReservationId { get; init; }

        /// <summary>
        /// Gets the AGV identifier to release resources for, if any.
        /// 获取要为其释放资源的 AGV 标识符（如果有）。
        /// </summary>
        public string? AgvId { get; init; }

        /// <summary>
        /// Gets the task identifier to release resources for, if any.
        /// 获取要为其释放资源的任务标识符（如果有）。
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the resources to release.
        /// 获取要释放的资源。
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the release reason.
        /// 获取释放原因。
        /// </summary>
        public string? Reason { get; init; }
    }
}
