using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Traffic.Enums;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Represents the result and lifecycle data of a traffic resource reservation.
    /// 表示交通资源预留的结果和生命周期数据。
    /// </summary>
    public sealed class TrafficReservationDto
    {
        /// <summary>
        /// Gets the reservation identifier.
        /// 获取预留标识符。
        /// </summary>
        public string ReservationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the AGV that owns the reservation.
        /// 获取拥有该预留的 AGV。
        /// </summary>
        public string AgvId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the task associated with the reservation, if any.
        /// 获取与该预留相关的任务（如果有）。
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the acquisition mode used for the reservation.
        /// 获取用于该预留的获取模式。
        /// </summary>
        public TrafficLockMode LockMode { get; init; } = TrafficLockMode.Reserve;

        /// <summary>
        /// Gets the resources covered by the reservation.
        /// 获取该预留涵盖的资源。
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the time when the reservation was created.
        /// 获取预留创建的时间。
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the expiration time of the reservation, if any.
        /// 获取预留的过期时间（如果有）。
        /// </summary>
        public DateTimeOffset? ExpireAt { get; init; }

        /// <summary>
        /// Gets the current reservation status.
        /// 获取当前预留状态。
        /// </summary>
        public TrafficReservationStatus Status { get; init; } = TrafficReservationStatus.Active;
    }
}
