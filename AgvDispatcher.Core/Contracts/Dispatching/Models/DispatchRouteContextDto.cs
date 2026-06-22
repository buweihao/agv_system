using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Planning.Results;

namespace AgvDispatcher.Core.Contracts.Dispatching.Models
{
    /// <summary>
    /// Carries the immutable route references required while orchestrating a task execution.
    /// </summary>
    public sealed class DispatchRouteContextDto
    {
        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the vehicle identifier.</summary>
        public string VehicleId { get; init; } = string.Empty;

        /// <summary>Gets the map identifier used for planning.</summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>Gets the map version used for planning.</summary>
        public string MapVersion { get; init; } = string.Empty;

        /// <summary>Gets the path plan identifier.</summary>
        public string PlanId { get; init; } = string.Empty;

        /// <summary>Gets the route reservation identifier.</summary>
        public string ReservationId { get; init; } = string.Empty;

        /// <summary>Gets the rolling lock window size.</summary>
        public int RollingWindowSize { get; init; }

        /// <summary>Gets the ordered path segments from the existing planning contract.</summary>
        public IReadOnlyList<PathSegmentDto> Segments { get; init; } = Array.Empty<PathSegmentDto>();
    }
}
