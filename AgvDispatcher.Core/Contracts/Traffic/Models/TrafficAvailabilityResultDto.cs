using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Represents the availability result for a set of traffic resources.
    /// </summary>
    public sealed class TrafficAvailabilityResultDto
    {
        /// <summary>
        /// Gets a value indicating whether all requested resources are available.
        /// </summary>
        public bool IsAvailable { get; init; }

        /// <summary>
        /// Gets the current statuses of the requested resources.
        /// </summary>
        public IReadOnlyList<TrafficResourceStatusDto> ResourceStatuses { get; init; } = Array.Empty<TrafficResourceStatusDto>();

        /// <summary>
        /// Gets conflicts that prevent the request from succeeding.
        /// </summary>
        public IReadOnlyList<TrafficConflictDto> Conflicts { get; init; } = Array.Empty<TrafficConflictDto>();

        /// <summary>
        /// Gets an optional human-readable availability message.
        /// </summary>
        public string? Message { get; init; }
    }
}
