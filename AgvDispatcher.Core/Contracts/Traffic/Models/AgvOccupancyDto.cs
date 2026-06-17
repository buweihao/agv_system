using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Describes the runtime resources currently occupied by one AGV.
    /// </summary>
    public sealed class AgvOccupancyDto
    {
        /// <summary>
        /// Gets the AGV identifier.
        /// </summary>
        public string AgvId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the task associated with the occupancy, if any.
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the current node identifier reported by the AGV, if any.
        /// </summary>
        public string? CurrentNodeId { get; init; }

        /// <summary>
        /// Gets the current edge identifier reported by the AGV, if any.
        /// </summary>
        public string? CurrentEdgeId { get; init; }

        /// <summary>
        /// Gets the occupied node identifiers.
        /// </summary>
        public IReadOnlyList<string> OccupiedNodeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the occupied edge identifiers.
        /// </summary>
        public IReadOnlyList<string> OccupiedEdgeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the AGV report time used to derive this occupancy.
        /// </summary>
        public DateTimeOffset ReportTime { get; init; } = DateTimeOffset.Now;
    }
}
