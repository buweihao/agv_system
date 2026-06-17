using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests release of traffic resources associated with a reservation, AGV, or task.
    /// </summary>
    public sealed class TrafficReleaseRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the reservation identifier to release, if any.
        /// </summary>
        public string? ReservationId { get; init; }

        /// <summary>
        /// Gets the AGV identifier to release resources for, if any.
        /// </summary>
        public string? AgvId { get; init; }

        /// <summary>
        /// Gets the task identifier to release resources for, if any.
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the resources to release.
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the release reason.
        /// </summary>
        public string? Reason { get; init; }
    }
}
