using AgvDispatcher.Core.Contracts.Dispatching.Models;

namespace AgvDispatcher.Core.Contracts.Dispatching.Results
{
    /// <summary>
    /// Describes the outcome of retrying an existing dispatch execution waiting for traffic.
    /// </summary>
    public sealed class RetryWaitingDispatchResultDto
    {
        /// <summary>Gets the updated dispatch execution.</summary>
        public DispatchExecutionDto Execution { get; init; } = new();

        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the vehicle identifier.</summary>
        public string VehicleId { get; init; } = string.Empty;

        /// <summary>Gets the existing path plan identifier.</summary>
        public string PlanId { get; init; } = string.Empty;

        /// <summary>Gets the existing route reservation identifier.</summary>
        public string ReservationId { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether the first rolling window was locked.</summary>
        public bool FirstWindowLocked { get; init; }

        /// <summary>Gets a value indicating whether the vehicle command was sent.</summary>
        public bool VehicleCommandSent { get; init; }

        /// <summary>Gets a value indicating whether traffic is still busy.</summary>
        public bool ShouldWait { get; init; }

        /// <summary>Gets a value indicating whether replanning is required.</summary>
        public bool RequiresReplan { get; init; }

        /// <summary>Gets the vehicle command identifier, if sent.</summary>
        public string? CommandId { get; init; }

        /// <summary>Gets an optional outcome message.</summary>
        public string? Message { get; init; }
    }
}
