using AgvDispatcher.Core.Contracts.Dispatching.Models;

namespace AgvDispatcher.Core.Contracts.Dispatching.Results
{
    /// <summary>
    /// Describes the outcome of replanning an existing dispatch execution.
    /// </summary>
    public sealed class ReplanDispatchTaskResultDto
    {
        /// <summary>Gets the updated dispatch execution.</summary>
        public DispatchExecutionDto Execution { get; init; } = new();

        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the vehicle identifier.</summary>
        public string VehicleId { get; init; } = string.Empty;

        /// <summary>Gets the old path plan identifier.</summary>
        public string? OldPlanId { get; init; }

        /// <summary>Gets the new path plan identifier.</summary>
        public string? NewPlanId { get; init; }

        /// <summary>Gets the old route reservation identifier.</summary>
        public string? OldReservationId { get; init; }

        /// <summary>Gets the new route reservation identifier.</summary>
        public string? NewReservationId { get; init; }

        /// <summary>Gets a value indicating whether the route changed.</summary>
        public bool RouteChanged { get; init; }

        /// <summary>Gets a value indicating whether the first rolling window was locked.</summary>
        public bool FirstWindowLocked { get; init; }

        /// <summary>Gets a value indicating whether another replanning attempt is required.</summary>
        public bool RequiresReplan { get; init; }

        /// <summary>Gets a value indicating whether execution should wait for traffic resources.</summary>
        public bool ShouldWait { get; init; }

        /// <summary>Gets an optional outcome message.</summary>
        public string? Message { get; init; }
    }
}
