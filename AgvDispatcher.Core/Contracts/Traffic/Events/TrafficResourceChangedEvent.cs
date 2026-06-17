using System;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Events
{
    /// <summary>
    /// Describes a runtime state change for a traffic-controlled resource.
    /// </summary>
    public sealed class TrafficResourceChangedEvent
    {
        /// <summary>
        /// Gets the traffic event type.
        /// </summary>
        public TrafficEventType EventType { get; init; }

        /// <summary>
        /// Gets the resource whose state changed.
        /// </summary>
        public TrafficResourceKey Resource { get; init; } = new TrafficResourceKey();

        /// <summary>
        /// Gets the previous resource state.
        /// </summary>
        public TrafficResourceState OldState { get; init; } = TrafficResourceState.Unknown;

        /// <summary>
        /// Gets the new resource state.
        /// </summary>
        public TrafficResourceState NewState { get; init; } = TrafficResourceState.Unknown;

        /// <summary>
        /// Gets the AGV related to the change, if any.
        /// </summary>
        public string? AgvId { get; init; }

        /// <summary>
        /// Gets the task related to the change, if any.
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the reservation related to the change, if any.
        /// </summary>
        public string? ReservationId { get; init; }

        /// <summary>
        /// Gets the reason for the change.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the time when the change occurred.
        /// </summary>
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.Now;
    }
}
