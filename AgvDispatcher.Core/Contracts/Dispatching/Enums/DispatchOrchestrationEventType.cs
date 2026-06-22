namespace AgvDispatcher.Core.Contracts.Dispatching.Enums
{
    /// <summary>
    /// Defines observable milestones in a dispatch orchestration execution.
    /// </summary>
    public enum DispatchOrchestrationEventType
    {
        /// <summary>The task was accepted for dispatch.</summary>
        TaskAccepted,
        /// <summary>A vehicle was selected.</summary>
        VehicleSelected,
        /// <summary>The runtime map was loaded.</summary>
        MapLoaded,
        /// <summary>The runtime traffic snapshot was loaded.</summary>
        TrafficSnapshotLoaded,
        /// <summary>A path was planned.</summary>
        PathPlanned,
        /// <summary>A route reservation was created.</summary>
        RouteReservationCreated,
        /// <summary>The first rolling route window was locked.</summary>
        FirstWindowLocked,
        /// <summary>A vehicle command was sent.</summary>
        VehicleCommandSent,
        /// <summary>The running route advanced.</summary>
        RouteAdvanced,
        /// <summary>Passed route resources were released.</summary>
        PassedResourcesReleased,
        /// <summary>The next rolling route window was locked.</summary>
        NextWindowLocked,
        /// <summary>The execution is waiting for traffic.</summary>
        WaitingForTraffic,
        /// <summary>The execution requires a new path plan.</summary>
        ReplanRequired,
        /// <summary>The task completed.</summary>
        TaskCompleted,
        /// <summary>The task began cancellation.</summary>
        TaskCanceling,
        /// <summary>The task was canceled.</summary>
        TaskCanceled,
        /// <summary>The dispatch execution failed.</summary>
        DispatchFailed
    }
}
