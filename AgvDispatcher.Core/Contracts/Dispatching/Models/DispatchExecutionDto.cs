using System;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;

namespace AgvDispatcher.Core.Contracts.Dispatching.Models
{
    /// <summary>
    /// Describes the current lifecycle and cross-module references of a dispatch execution.
    /// </summary>
    public sealed class DispatchExecutionDto
    {
        /// <summary>Gets the execution identifier.</summary>
        public string ExecutionId { get; init; } = string.Empty;

        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the selected vehicle identifier.</summary>
        public string? VehicleId { get; init; }

        /// <summary>Gets the current orchestration state.</summary>
        public DispatchExecutionState State { get; init; }

        /// <summary>Gets the runtime map identifier.</summary>
        public string? MapId { get; init; }

        /// <summary>Gets the runtime map version.</summary>
        public string? MapVersion { get; init; }

        /// <summary>Gets the path plan identifier.</summary>
        public string? PlanId { get; init; }

        /// <summary>Gets the route reservation identifier.</summary>
        public string? ReservationId { get; init; }

        /// <summary>Gets the vehicle's current node identifier.</summary>
        public string? CurrentNodeId { get; init; }

        /// <summary>Gets the current route segment sequence.</summary>
        public int CurrentSegmentSequence { get; init; }

        /// <summary>Gets the configured rolling lock window size.</summary>
        public int RollingWindowSize { get; init; }

        /// <summary>Gets the execution creation time.</summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>Gets the last update time.</summary>
        public DateTimeOffset UpdatedAt { get; init; }

        /// <summary>Gets the last orchestration failure code, if any.</summary>
        public string? LastFailureCode { get; init; }

        /// <summary>Gets the last orchestration failure message, if any.</summary>
        public string? LastFailureMessage { get; init; }
    }
}
