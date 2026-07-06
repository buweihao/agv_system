namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public sealed class MockSimulationTickResult
    {
        public long Tick { get; init; }

        public bool Succeeded { get; init; } = true;

        public string Message { get; init; } = string.Empty;

        public IReadOnlyList<MockSimulationEvent> Events { get; init; } =
            Array.Empty<MockSimulationEvent>();

        public IReadOnlyList<MockVehicleRuntimeState> VehicleStates { get; init; } =
            Array.Empty<MockVehicleRuntimeState>();
    }
}
