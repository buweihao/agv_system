using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Dispatching.Requests
{
    /// <summary>
    /// Requests replanning for an existing dispatch execution.
    /// </summary>
    public sealed class ReplanDispatchTaskRequest : IAgvRequest
    {
        /// <summary>Gets the request context.</summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the vehicle identifier.</summary>
        public string VehicleId { get; init; } = string.Empty;

        /// <summary>Gets the vehicle's current node identifier.</summary>
        public string CurrentNodeId { get; init; } = string.Empty;

        /// <summary>Gets the highest current route segment sequence already passed.</summary>
        public int CurrentSegmentSequence { get; init; }

        /// <summary>Gets the replanning reason.</summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>Gets route edge identifiers that must be excluded from the new plan.</summary>
        public IReadOnlyList<string> ForbiddenEdgeIds { get; init; } = new List<string>();

        /// <summary>Gets route node identifiers that must be excluded from the new plan.</summary>
        public IReadOnlyList<string> ForbiddenNodeIds { get; init; } = new List<string>();

        /// <summary>Gets a value indicating whether occupied traffic resources should be avoided.</summary>
        public bool AvoidOccupiedResources { get; init; } = true;

        /// <summary>Gets a value indicating whether the old route reservation should be released.</summary>
        public bool ReleaseOldReservation { get; init; } = true;

        /// <summary>Gets a value indicating whether the first rolling window should be acquired.</summary>
        public bool AcquireFirstWindow { get; init; } = true;
    }
}
