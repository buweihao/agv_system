using System;
using AgvDispatcher.Core.Contracts.Traffic.Enums;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Describes the current runtime state of one traffic resource.
    /// 描述一个交通资源的当前运行时状态。
    /// </summary>
    public sealed class TrafficResourceStatusDto
    {
        /// <summary>
        /// Gets the resource whose state is described.
        /// 获取描述其状态的资源。
        /// </summary>
        public TrafficResourceKey Resource { get; init; } = new TrafficResourceKey();

        /// <summary>
        /// Gets the current resource state.
        /// 获取当前资源状态。
        /// </summary>
        public TrafficResourceState State { get; init; } = TrafficResourceState.Unknown;

        /// <summary>
        /// Gets the AGV currently occupying the resource, if any.
        /// 获取当前占用该资源的 AGV（如果有）。
        /// </summary>
        public string? OccupiedByAgvId { get; init; }

        /// <summary>
        /// Gets the AGV currently reserving the resource, if any.
        /// 获取当前预留该资源的 AGV（如果有）。
        /// </summary>
        public string? ReservedByAgvId { get; init; }

        /// <summary>
        /// Gets the task associated with the current state, if any.
        /// 获取与当前状态相关的任务（如果有）。
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the reservation identifier associated with the current state, if any.
        /// 获取与当前状态相关的预留标识符（如果有）。
        /// </summary>
        public string? ReservationId { get; init; }

        /// <summary>
        /// Gets the reason for a lock, block, or state transition.
        /// 获取锁定、阻塞或状态转换的原因。
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the expiration time for temporary state, if any.
        /// 获取临时状态的过期时间（如果有）。
        /// </summary>
        public DateTimeOffset? ExpireAt { get; init; }

        /// <summary>
        /// Gets the last time this status was updated.
        /// 获取上次更新此状态的时间。
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
    }
}
