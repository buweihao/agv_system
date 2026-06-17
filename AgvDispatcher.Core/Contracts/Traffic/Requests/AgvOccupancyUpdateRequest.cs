using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests an update of traffic occupancy based on an AGV status report.
    /// </summary>
    public sealed class AgvOccupancyUpdateRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the AGV identifier that reported occupancy.
        /// </summary>
        public string AgvId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the task associated with the occupancy report, if any.
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
        /// Gets the node identifiers currently occupied by the AGV.
        /// </summary>
        public IReadOnlyList<string> OccupiedNodeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the edge identifiers currently occupied by the AGV.
        /// </summary>
        public IReadOnlyList<string> OccupiedEdgeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the AGV report time.
        /// </summary>
        public DateTimeOffset ReportTime { get; init; } = DateTimeOffset.Now;
    }
}
