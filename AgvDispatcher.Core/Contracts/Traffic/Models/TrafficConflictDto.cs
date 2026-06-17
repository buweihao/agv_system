using AgvDispatcher.Core.Contracts.Traffic.Enums;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Describes one conflict found during a traffic availability or acquisition check.
    /// </summary>
    public sealed class TrafficConflictDto
    {
        /// <summary>
        /// Gets the conflicted resource.
        /// </summary>
        public TrafficResourceKey Resource { get; init; } = new TrafficResourceKey();

        /// <summary>
        /// Gets the current state that caused the conflict.
        /// </summary>
        public TrafficResourceState CurrentState { get; init; } = TrafficResourceState.Unknown;

        /// <summary>
        /// Gets the conflicting AGV identifier, if known.
        /// </summary>
        public string? ConflictAgvId { get; init; }

        /// <summary>
        /// Gets the conflicting task identifier, if known.
        /// </summary>
        public string? ConflictTaskId { get; init; }

        /// <summary>
        /// Gets the conflicting reservation identifier, if known.
        /// </summary>
        public string? ReservationId { get; init; }

        /// <summary>
        /// Gets the traffic-specific failure code.
        /// </summary>
        public TrafficFailureCode FailureCode { get; init; } = TrafficFailureCode.None;

        /// <summary>
        /// Gets an optional conflict message.
        /// </summary>
        public string? Message { get; init; }
    }
}
