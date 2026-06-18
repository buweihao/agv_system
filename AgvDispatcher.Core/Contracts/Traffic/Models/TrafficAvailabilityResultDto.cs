using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Represents the availability result for a set of traffic resources.
    /// 表示一组交通资源的可用性结果。
    /// </summary>
    public sealed class TrafficAvailabilityResultDto
    {
        /// <summary>
        /// Gets a value indicating whether all requested resources are available.
        /// 获取一个值，该值指示是否所有请求的资源都可用。
        /// </summary>
        public bool IsAvailable { get; init; }

        /// <summary>
        /// Gets the current statuses of the requested resources.
        /// 获取请求资源的当前状态。
        /// </summary>
        public IReadOnlyList<TrafficResourceStatusDto> ResourceStatuses { get; init; } = Array.Empty<TrafficResourceStatusDto>();

        /// <summary>
        /// Gets conflicts that prevent the request from succeeding.
        /// 获取阻止请求成功的冲突。
        /// </summary>
        public IReadOnlyList<TrafficConflictDto> Conflicts { get; init; } = Array.Empty<TrafficConflictDto>();

        /// <summary>
        /// Gets an optional human-readable availability message.
        /// 获取可选的人类可读可用性消息。
        /// </summary>
        public string? Message { get; init; }
    }
}
