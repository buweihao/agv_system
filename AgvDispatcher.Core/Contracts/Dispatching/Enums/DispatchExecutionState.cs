namespace AgvDispatcher.Core.Contracts.Dispatching.Enums
{
    /// <summary>
    /// Defines the lifecycle states of a dispatch orchestration execution.
    /// </summary>
    public enum DispatchExecutionState
    {
        /// <summary>No execution state has been assigned.</summary>
        None = 0,
        /// <summary>The execution record has been created.</summary>
        Created = 10,
        /// <summary>A suitable vehicle is being selected.</summary>
        SelectingVehicle = 20,
        /// <summary>A route is being planned.</summary>
        PlanningPath = 30,
        /// <summary>A route reservation is being created.</summary>
        ReservingRoute = 40,
        /// <summary>The first rolling route window is being locked.</summary>
        LockingFirstWindow = 50,
        /// <summary>The execution is ready for command dispatch.</summary>
        ReadyToDispatch = 60,
        /// <summary>The initial vehicle command has been sent.</summary>
        CommandSent = 70,
        /// <summary>The vehicle is executing the route.</summary>
        Running = 80,
        /// <summary>The execution is waiting for traffic resources.</summary>
        WaitingForTraffic = 90,
        /// <summary>The route is being replanned.</summary>
        Replanning = 100,
        /// <summary>The task execution completed successfully.</summary>
        Completed = 200,
        /// <summary>The task execution is being canceled.</summary>
        Canceling = 300,
        /// <summary>The task execution was canceled.</summary>
        Canceled = 310,
        /// <summary>The task execution failed.</summary>
        Failed = 400
    }
}
