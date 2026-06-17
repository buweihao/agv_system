using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Traffic.Enums;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Represents the result and lifecycle data of a traffic resource reservation.
    /// </summary>
    public sealed class TrafficReservationDto
    {
        /// <summary>
        /// Gets the reservation identifier.
        /// </summary>
        public string ReservationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the AGV that owns the reservation.
        /// </summary>
        public string AgvId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the task associated with the reservation, if any.
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the acquisition mode used for the reservation.
        /// </summary>
        public TrafficLockMode LockMode { get; init; } = TrafficLockMode.Reserve;

        /// <summary>
        /// Gets the resources covered by the reservation.
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the time when the reservation was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the expiration time of the reservation, if any.
        /// </summary>
        public DateTimeOffset? ExpireAt { get; init; }

        /// <summary>
        /// Gets the current reservation status.
        /// </summary>
        public TrafficReservationStatus Status { get; init; } = TrafficReservationStatus.Active;
    }
}
