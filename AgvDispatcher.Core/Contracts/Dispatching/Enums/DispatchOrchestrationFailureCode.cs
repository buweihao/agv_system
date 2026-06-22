namespace AgvDispatcher.Core.Contracts.Dispatching.Enums
{
    /// <summary>
    /// Defines failures produced specifically by dispatch orchestration.
    /// </summary>
    public enum DispatchOrchestrationFailureCode
    {
        /// <summary>No failure occurred.</summary>
        None = 0,
        /// <summary>The request is invalid.</summary>
        InvalidRequest = 100,
        /// <summary>The requested task does not exist.</summary>
        TaskNotFound = 200,
        /// <summary>The task cannot currently be dispatched.</summary>
        TaskNotDispatchable = 210,
        /// <summary>The requested or selected vehicle does not exist.</summary>
        VehicleNotFound = 300,
        /// <summary>No suitable vehicle is currently available.</summary>
        VehicleNotAvailable = 310,
        /// <summary>The current runtime map is unavailable.</summary>
        MapUnavailable = 400,
        /// <summary>The expected and current map versions differ.</summary>
        MapVersionMismatch = 410,
        /// <summary>The requested destination cannot be reached.</summary>
        PathNotReachable = 500,
        /// <summary>Path planning failed.</summary>
        PathPlanningFailed = 510,
        /// <summary>Route reservation creation failed.</summary>
        RouteReservationFailed = 600,
        /// <summary>The first rolling window could not be acquired.</summary>
        FirstWindowAcquireFailed = 610,
        /// <summary>A required traffic resource is busy.</summary>
        TrafficResourceBusy = 620,
        /// <summary>The current route must be replanned.</summary>
        ReplanRequired = 630,
        /// <summary>Sending a vehicle command failed.</summary>
        VehicleCommandFailed = 700,
        /// <summary>Canceling the dispatch execution failed.</summary>
        CancelFailed = 800,
        /// <summary>An unexpected orchestration error occurred.</summary>
        InternalError = 900
    }
}
