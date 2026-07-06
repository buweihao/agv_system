namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public interface IMockFleetSimulationEngine
    {
        MockSimulationScenario? CurrentScenario { get; }

        IReadOnlyList<MockVehicleRuntimeState> GetVehicleStates();

        MockVehicleRuntimeState? GetVehicleState(string vehicleId);

        Task<MockSimulationTickResult> InitializeAsync(
            MockSimulationScenario scenario,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> StartTaskAsync(
            string vehicleId,
            string taskId,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> StartAllAsync(CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> StepAsync(CancellationToken cancellationToken = default);
    }
}
