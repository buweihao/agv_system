namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public sealed class MockVehicleRuntimeState
    {
        public string VehicleId { get; init; } = string.Empty;

        public string Brand { get; init; } = "Mock";

        public MockVehicleSimulationState State { get; init; }

        public string CurrentNodeId { get; init; } = string.Empty;

        public string? CurrentEdgeId { get; init; }

        public string? CurrentTaskId { get; init; }

        public string? TargetNodeId { get; init; }

        public int CurrentSegmentSequence { get; init; }

        public string? ReservationId { get; init; }

        public string? PlanId { get; init; }

        public DateTimeOffset? WaitingSince { get; init; }

        public DateTimeOffset? LastRetryAt { get; init; }

        public int WaitRetryCount { get; init; }

        public double BatteryLevel { get; init; } = 100;

        public bool IsOnline { get; init; } = true;

        public bool HasFault { get; init; }

        public string? FaultCode { get; init; }

        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;

        public MockVehicleRuntimeState Clone() => new()
        {
            VehicleId = VehicleId,
            Brand = Brand,
            State = State,
            CurrentNodeId = CurrentNodeId,
            CurrentEdgeId = CurrentEdgeId,
            CurrentTaskId = CurrentTaskId,
            TargetNodeId = TargetNodeId,
            CurrentSegmentSequence = CurrentSegmentSequence,
            ReservationId = ReservationId,
            PlanId = PlanId,
            WaitingSince = WaitingSince,
            LastRetryAt = LastRetryAt,
            WaitRetryCount = WaitRetryCount,
            BatteryLevel = BatteryLevel,
            IsOnline = IsOnline,
            HasFault = HasFault,
            FaultCode = FaultCode,
            UpdatedAt = UpdatedAt
        };
    }

    public enum MockVehicleSimulationState
    {
        Idle = 0,
        Dispatching = 10,
        Running = 20,
        WaitingForTraffic = 30,
        Replanning = 40,
        Completed = 50,
        Canceled = 60,
        Fault = 70,
        Failed = 80,
        TimedOut = 90
    }
}
