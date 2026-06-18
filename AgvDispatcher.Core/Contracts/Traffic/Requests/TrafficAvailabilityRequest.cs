using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests an availability check for traffic resources.
    /// 请求对交通资源进行可用性检查。
    /// </summary>
    public sealed class TrafficAvailabilityRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// 获取请求上下文。
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the AGV identifier for the check, if any.
        /// 获取用于检查的 AGV 标识符（如果有）。
        /// </summary>
        public string? AgvId { get; init; }

        /// <summary>
        /// Gets the task identifier for the check, if any.
        /// 获取用于检查的任务标识符（如果有）。
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the resources to check.
        /// 获取要检查的资源。
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the expected map version, if the caller requires version consistency.
        /// 获取预期的地图版本（如果调用者需要版本一致性）。
        /// </summary>
        public string? ExpectedMapVersion { get; init; }
    }
}
