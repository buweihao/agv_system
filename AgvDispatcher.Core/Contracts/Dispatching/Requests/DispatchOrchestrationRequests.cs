using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Dispatching.Requests
{
    /// <summary>
    /// Requests creation and startup of a task dispatch execution.
    /// </summary>
    public sealed class StartDispatchTaskRequest : IAgvRequest
    {
        /// <summary>Gets the request context.</summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets an optional preferred vehicle identifier.</summary>
        public string? PreferredVehicleId { get; init; }

        /// <summary>Gets the number of route segments in each rolling lock window.</summary>
        public int RollingWindowSize { get; init; } = 2;

        /// <summary>Gets the maximum number of replanning attempts.</summary>
        public int MaxReplanCount { get; init; } = 1;

        /// <summary>Gets a value indicating whether the initial vehicle command should be sent.</summary>
        public bool SendVehicleCommand { get; init; } = true;
    }

    /// <summary>
    /// Requests advancement of a running dispatch route.
    /// </summary>
    public sealed class AdvanceDispatchRouteRequest : IAgvRequest
    {
        /// <summary>Gets the request context.</summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the vehicle identifier.</summary>
        public string VehicleId { get; init; } = string.Empty;

        /// <summary>Gets the vehicle's current node identifier, if known.</summary>
        public string? CurrentNodeId { get; init; }

        /// <summary>Gets the vehicle's current edge identifier, if known.</summary>
        public string? CurrentEdgeId { get; init; }

        /// <summary>Gets the highest route segment sequence already passed.</summary>
        public int PassedSegmentSequence { get; init; }

        /// <summary>Gets a value indicating whether the next rolling window should be acquired.</summary>
        public bool AcquireNextWindow { get; init; } = true;
    }

    /// <summary>
    /// Requests cancellation of a task dispatch execution.
    /// </summary>
    public sealed class CancelDispatchTaskRequest : IAgvRequest
    {
        /// <summary>Gets the request context.</summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the cancellation reason.</summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether the route reservation should be released.</summary>
        public bool ReleaseReservation { get; init; } = true;

        /// <summary>Gets a value indicating whether a cancellation command should be sent to the vehicle.</summary>
        public bool SendCancelCommand { get; init; } = true;
    }

    /// <summary>
    /// Requests completion of a dispatch execution after the vehicle reaches its destination.
    /// </summary>
    public sealed class CompleteDispatchTaskRequest : IAgvRequest
    {
        /// <summary>Gets the request context.</summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>Gets the completed task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the vehicle identifier reporting completion.</summary>
        public string VehicleId { get; init; } = string.Empty;

        /// <summary>Gets the final node reported by the vehicle.</summary>
        public string? CurrentNodeId { get; init; }

        /// <summary>Gets a value indicating whether the route reservation should be released.</summary>
        public bool ReleaseReservation { get; init; } = true;
    }

    /// <summary>
    /// Requests the current dispatch execution for a task.
    /// </summary>
    public sealed class GetDispatchExecutionRequest : IAgvRequest
    {
        /// <summary>Gets the request context.</summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets an optional execution identifier used to disambiguate task executions.</summary>
        public string? ExecutionId { get; init; }
    }

    /// <summary>
    /// Requests the current in-memory dispatch executions.
    /// </summary>
    public sealed class GetDispatchExecutionsRequest : IAgvRequest
    {
        /// <summary>Gets the request context.</summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>Gets an optional task identifier filter.</summary>
        public string? TaskId { get; init; }

        /// <summary>Gets an optional vehicle identifier filter.</summary>
        public string? VehicleId { get; init; }
    }
}
