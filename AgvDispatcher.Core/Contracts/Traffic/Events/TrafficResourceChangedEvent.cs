using System;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Events
{
    /// <summary>
    /// Describes a runtime state change for a traffic-controlled resource.
    /// 描述受交通控制资源的运行时状态改变。
    /// </summary>
    public sealed class TrafficResourceChangedEvent
    {
        /// <summary>
        /// Gets the traffic event type.
        /// 获取交通事件类型。
        /// </summary>
        public TrafficEventType EventType { get; init; }

        /// <summary>
        /// Gets the resource whose state changed.
        /// 获取状态已更改的资源。
        /// </summary>
        public TrafficResourceKey Resource { get; init; } = new TrafficResourceKey();

        /// <summary>
        /// Gets the previous resource state.
        /// 获取以前的资源状态。
        /// </summary>
        public TrafficResourceState OldState { get; init; } = TrafficResourceState.Unknown;

        /// <summary>
        /// Gets the new resource state.
        /// 获取新的资源状态。
        /// </summary>
        public TrafficResourceState NewState { get; init; } = TrafficResourceState.Unknown;

        /// <summary>
        /// Gets the AGV related to the change, if any.
        /// 获取与更改相关的 AGV（如果有）。
        /// </summary>
        public string? AgvId { get; init; }

        /// <summary>
        /// Gets the task related to the change, if any.
        /// 获取与更改相关的任务（如果有）。
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the reservation related to the change, if any.
        /// 获取与更改相关的预留（如果有）。
        /// </summary>
        public string? ReservationId { get; init; }

        /// <summary>
        /// Gets the reason for the change.
        /// 获取更改的原因。
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the time when the change occurred.
        /// 获取更改发生的时间。
        /// </summary>
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.Now;
    }
}
