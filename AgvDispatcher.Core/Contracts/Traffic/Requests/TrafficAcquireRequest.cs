using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests reservation, occupancy, lock, or block acquisition for traffic resources.
    /// </summary>
    public sealed class TrafficAcquireRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the AGV identifier that acquires the resources.
        /// </summary>
        public string AgvId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the task identifier associated with the acquisition, if any.
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the acquisition mode.
        /// </summary>
        public TrafficLockMode LockMode { get; init; } = TrafficLockMode.Reserve;

        /// <summary>
        /// Gets the resources to acquire.
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the time-to-live for temporary acquisition, if any.
        /// </summary>
        public TimeSpan? Ttl { get; init; }

        /// <summary>
        /// Gets the reason for the acquisition.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the expected map version, if the caller requires version consistency.
        /// </summary>
        public string? ExpectedMapVersion { get; init; }
    }
}
