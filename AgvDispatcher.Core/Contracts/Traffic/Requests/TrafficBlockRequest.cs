using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests a manual or operational block for traffic resources.
    /// </summary>
    public sealed class TrafficBlockRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the resources to block.
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the block reason.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the operator identifier that requested the block, if any.
        /// </summary>
        public string? OperatorId { get; init; }

        /// <summary>
        /// Gets the time-to-live for the block, if any.
        /// </summary>
        public TimeSpan? Ttl { get; init; }
    }
}
