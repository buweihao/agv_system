using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests reservation, occupancy, lock, or block acquisition for traffic resources.
    /// 请求获取交通资源的预留、占用、锁定或阻塞。
    /// </summary>
    public sealed class TrafficAcquireRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// 获取请求上下文。
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the AGV identifier that acquires the resources.
        /// 获取获取资源的 AGV 标识符。
        /// </summary>
        public string AgvId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the task identifier associated with the acquisition, if any.
        /// 获取与获取相关的任务标识符（如果有）。
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the acquisition mode.
        /// 获取获取模式。
        /// </summary>
        public TrafficLockMode LockMode { get; init; } = TrafficLockMode.Reserve;

        /// <summary>
        /// Gets the resources to acquire.
        /// 获取要获取的资源。
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the time-to-live for temporary acquisition, if any.
        /// 获取临时获取的生存时间（如果有）。
        /// </summary>
        public TimeSpan? Ttl { get; init; }

        /// <summary>
        /// Gets the reason for the acquisition.
        /// 获取获取原因。
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the expected map version, if the caller requires version consistency.
        /// 获取预期的地图版本（如果调用者需要版本一致性）。
        /// </summary>
        public string? ExpectedMapVersion { get; init; }
    }
}
