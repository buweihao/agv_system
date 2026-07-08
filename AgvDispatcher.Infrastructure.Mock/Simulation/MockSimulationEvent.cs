namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public sealed class MockSimulationEvent
    {
        public long Tick { get; init; }

        public MockSimulationEventType EventType { get; init; }

        public string? VehicleId { get; init; }

        public string? TaskId { get; init; }

        public string? FromNodeId { get; init; }

        public string? ToNodeId { get; init; }

        public string? ResourceId { get; init; }

        public string? OldPlanId { get; init; }

        public string? NewPlanId { get; init; }

        public string? OldReservationId { get; init; }

        public string? NewReservationId { get; init; }

        public string? Reason { get; init; }

        public string Message { get; init; } = string.Empty;

        public static MockSimulationEvent VehicleInitialized(
            long tick,
            string vehicleId,
            string? taskId,
            string nodeId) => new()
        {
            Tick = tick,
            EventType = MockSimulationEventType.VehicleInitialized,
            VehicleId = vehicleId,
            TaskId = taskId,
            ToNodeId = nodeId,
            Message = $"Vehicle {vehicleId} initialized at {nodeId}."
        };
    }

    public enum MockSimulationEventType
    {
        None = 0,
        VehicleInitialized = 10,
        TaskDispatchRequested = 20,
        TaskDispatchStarted = 30,
        VehicleMoved = 40,
        ResourceLocked = 50,
        ResourceReleased = 60,
        WaitingForTraffic = 70,
        WaitingTimedOut = 80,
        ReplanRequired = 90,
        TaskCompleted = 100,
        TaskCanceled = 110,
        VehicleFaulted = 120,
        RetryAttempted = 130,
        VehicleRecovered = 140,
        TaskReplanned = 150,
        RouteInvalidated = 160,
        ReplanFailed = 170,
        ReplanSkipped = 180,
        SimulationFailed = 900,
        NoOp = 1000
    }
}
