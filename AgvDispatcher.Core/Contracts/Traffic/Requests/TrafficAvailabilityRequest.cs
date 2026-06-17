using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests an availability check for traffic resources.
    /// </summary>
    public sealed class TrafficAvailabilityRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the AGV identifier for the check, if any.
        /// </summary>
        public string? AgvId { get; init; }

        /// <summary>
        /// Gets the task identifier for the check, if any.
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the resources to check.
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the expected map version, if the caller requires version consistency.
        /// </summary>
        public string? ExpectedMapVersion { get; init; }
    }
}
