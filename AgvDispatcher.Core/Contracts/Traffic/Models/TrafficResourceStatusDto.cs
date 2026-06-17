using System;
using AgvDispatcher.Core.Contracts.Traffic.Enums;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Describes the current runtime state of one traffic resource.
    /// </summary>
    public sealed class TrafficResourceStatusDto
    {
        /// <summary>
        /// Gets the resource whose state is described.
        /// </summary>
        public TrafficResourceKey Resource { get; init; } = new TrafficResourceKey();

        /// <summary>
        /// Gets the current resource state.
        /// </summary>
        public TrafficResourceState State { get; init; } = TrafficResourceState.Unknown;

        /// <summary>
        /// Gets the AGV currently occupying the resource, if any.
        /// </summary>
        public string? OccupiedByAgvId { get; init; }

        /// <summary>
        /// Gets the AGV currently reserving the resource, if any.
        /// </summary>
        public string? ReservedByAgvId { get; init; }

        /// <summary>
        /// Gets the task associated with the current state, if any.
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the reservation identifier associated with the current state, if any.
        /// </summary>
        public string? ReservationId { get; init; }

        /// <summary>
        /// Gets the reason for a lock, block, or state transition.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the expiration time for temporary state, if any.
        /// </summary>
        public DateTimeOffset? ExpireAt { get; init; }

        /// <summary>
        /// Gets the last time this status was updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
    }
}
