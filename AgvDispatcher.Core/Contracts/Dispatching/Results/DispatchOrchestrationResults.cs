using AgvDispatcher.Core.Contracts.Dispatching.Models;

namespace AgvDispatcher.Core.Contracts.Dispatching.Results
{
    /// <summary>
    /// Describes the outcome of starting a task dispatch execution.
    /// </summary>
    public sealed class StartDispatchTaskResultDto
    {
        /// <summary>Gets the resulting dispatch execution.</summary>
        public DispatchExecutionDto Execution { get; init; } = new();

        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the selected vehicle identifier.</summary>
        public string VehicleId { get; init; } = string.Empty;

        /// <summary>Gets the generated path plan identifier.</summary>
        public string PlanId { get; init; } = string.Empty;

        /// <summary>Gets the created route reservation identifier.</summary>
        public string ReservationId { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether a path was planned.</summary>
        public bool PathPlanned { get; init; }

        /// <summary>Gets a value indicating whether a route reservation was created.</summary>
        public bool ReservationCreated { get; init; }

        /// <summary>Gets a value indicating whether the first rolling window was locked.</summary>
        public bool FirstWindowLocked { get; init; }

        /// <summary>Gets a value indicating whether the vehicle command was sent.</summary>
        public bool VehicleCommandSent { get; init; }

        /// <summary>Gets the vehicle command identifier, if a command was sent.</summary>
        public string? CommandId { get; init; }

        /// <summary>Gets an optional outcome message.</summary>
        public string? Message { get; init; }
    }

    /// <summary>
    /// Describes the outcome of advancing a running dispatch route.
    /// </summary>
    public sealed class AdvanceDispatchRouteResultDto
    {
        /// <summary>Gets the updated dispatch execution.</summary>
        public DispatchExecutionDto Execution { get; init; } = new();

        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the vehicle identifier.</summary>
        public string VehicleId { get; init; } = string.Empty;

        /// <summary>Gets the highest route segment sequence released by this operation.</summary>
        public int ReleasedUpToSegmentSequence { get; init; }

        /// <summary>Gets a value indicating whether passed traffic resources were released.</summary>
        public bool ReleasedPassedResources { get; init; }

        /// <summary>Gets a value indicating whether the next rolling window was acquired.</summary>
        public bool AcquiredNextWindow { get; init; }

        /// <summary>Gets a value indicating whether execution should wait for traffic resources.</summary>
        public bool ShouldWait { get; init; }

        /// <summary>Gets a value indicating whether a new path plan is required.</summary>
        public bool RequiresReplan { get; init; }

        /// <summary>Gets an optional outcome message.</summary>
        public string? Message { get; init; }
    }
}
