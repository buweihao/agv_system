using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests removal of a manual or operational block from traffic resources.
    /// </summary>
    public sealed class TrafficUnblockRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the resources to unblock.
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the unblock reason.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the operator identifier that requested the unblock, if any.
        /// </summary>
        public string? OperatorId { get; init; }
    }
}
